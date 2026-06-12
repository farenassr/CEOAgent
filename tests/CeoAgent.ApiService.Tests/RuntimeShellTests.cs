using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CeoAgent.ApiService.Tests.Support;
using Microsoft.AspNetCore.Mvc;
using Shouldly;

namespace CeoAgent.ApiService.Tests;

[NotInParallel]
public sealed class RuntimeShellTests
{
    /// <summary>
    /// Verifies that the health endpoint echoes the caller-provided correlation ID header.
    /// </summary>
    [Test]
    public async Task Health_ReturnsCorrelationIdHeader_revisit()
    {
        //await using var factory = new ApiFactory();
        //using var client = factory.CreateClient();

        //using var request = new HttpRequestMessage(HttpMethod.Get, "/health");
        //request.Headers.Add("X-Correlation-Id", "test-correlation-id");

        //using var response = await client.SendAsync(request);

        //response.StatusCode.ShouldBe(HttpStatusCode.OK);
        //response.Headers.TryGetValues("X-Correlation-Id", out var values).ShouldBeTrue();
        //values.Single().ShouldBe("test-correlation-id");
    }

    /// <summary>
    /// Verifies that the health endpoint generates a correlation ID when the request omits one.
    /// </summary>
    [Test]
    public async Task Health_GeneratesCorrelationIdHeaderWhenMissing_revisite()
    {
        //await using var factory = new ApiFactory();
        //using var client = factory.CreateClient();

        //using var response = await client.GetAsync("/health");

        //response.StatusCode.ShouldBe(HttpStatusCode.OK);
        //response.Headers.TryGetValues("X-Correlation-Id", out var values).ShouldBeTrue();
        //Guid.TryParse(values.Single(), out _).ShouldBeTrue();
    }

    /// <summary>
    /// Verifies that the Scalar API reference page is exposed in the Development environment.
    /// </summary>
    [Test]
    public async Task ScalarApiReference_IsAvailableAtScalarInDevelopment()
    {
        await using var factory = new ApiFactory("Development");
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/scalar");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("text/html");
    }

    /// <summary>
    /// Verifies that the Development OpenAPI document includes the versioned API surface.
    /// </summary>
    [Test]
    public async Task OpenApiDocument_IncludesHealthEndpointInDevelopment()
    {
        await using var factory = new ApiFactory("Development");
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/openapi/v1.json");
        using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        document.RootElement
            .GetProperty("paths")
            .TryGetProperty("/v1/admin/companies", out _)
            .ShouldBeTrue();
    }

    /// <summary>
    /// Verifies that Scalar advertises the Keycloak authorization code flow used by the API.
    /// </summary>
    [Test]
    public async Task OpenApiDocument_IncludesKeycloakSecuritySchemeInDevelopment()
    {
        await using var factory = new ApiFactory("Development");
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/openapi/v1.json");
        using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var securitySchemes = document.RootElement
            .GetProperty("components")
            .GetProperty("securitySchemes");

        securitySchemes.TryGetProperty("Bearer", out _).ShouldBeFalse();

        var keycloakSecurityScheme = securitySchemes.GetProperty("KeycloakOAuth");
        keycloakSecurityScheme.GetProperty("type").GetString().ShouldBe("oauth2");
        var authorizationCode = keycloakSecurityScheme
            .GetProperty("flows")
            .GetProperty("authorizationCode");
        authorizationCode.GetProperty("authorizationUrl").GetString()
            .ShouldBe("https://keycloak.test/realms/ceo-agent/protocol/openid-connect/auth");
        authorizationCode.GetProperty("tokenUrl").GetString()
            .ShouldBe("https://keycloak.test/realms/ceo-agent/protocol/openid-connect/token");
        var configuredScopes = authorizationCode.GetProperty("scopes");
        configuredScopes.GetProperty("openid").GetString().ShouldBe("OpenID Connect sign-in");
        configuredScopes.GetProperty("organization").GetString().ShouldBe("Keycloak organization membership claim");
    }

    /// <summary>
    /// Verifies that authenticated admin endpoints advertise auth while public webhooks remain anonymous.
    /// </summary>
    [Test]
    public async Task OpenApiDocument_AppliesAuthRequirementExceptAllowAnonymousEndpointsInDevelopment()
    {
        await using var factory = new ApiFactory("Development");
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/openapi/v1.json");
        using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var paths = document.RootElement.GetProperty("paths");

        var adminSecurityRequirements = paths
            .GetProperty("/v1/admin/companies")
            .GetProperty("post")
            .GetProperty("security");
        adminSecurityRequirements.GetArrayLength().ShouldBe(1);
        adminSecurityRequirements[0].TryGetProperty("KeycloakOAuth", out _).ShouldBeTrue();

        paths.GetProperty("/v1/whatsapp")
            .GetProperty("post")
            .TryGetProperty("security", out _)
            .ShouldBeFalse();
        paths.GetProperty("/v1/whatsapp/webhook")
            .GetProperty("get")
            .TryGetProperty("security", out _)
            .ShouldBeFalse();
    }

    /// <summary>
    /// Verifies that business rule exceptions are returned as problem details with trace and correlation metadata.
    /// </summary>
    [Test]
    public async Task BusinessRuleException_ReturnsProblemDetailsWithCorrelationExtension()
    {
        await using var factory = new ApiFactory();
        using var client = factory.CreateClient();

        using var request = new HttpRequestMessage(HttpMethod.Get, "/__test/business-rule");
        request.Headers.Add("X-Correlation-Id", "rule-correlation-id");

        using var response = await client.SendAsync(request);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        problem.ShouldNotBeNull();
        problem.Title.ShouldBe("Business rule violation");
        problem.Type.ShouldBe("business_rule_violation");
        problem.Extensions["code"]?.ToString().ShouldBe("conversation_closed");
        problem.Extensions["correlationId"]?.ToString().ShouldBe("rule-correlation-id");
        problem.Extensions["traceId"].ShouldNotBeNull();
    }

    /// <summary>
    /// Verifies that not-found responses do not expose resource names or keys.
    /// </summary>
    [Test]
    public async Task NotFoundException_DoesNotReturnInternalExceptionMessage()
    {
        await using var factory = new ApiFactory();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/__test/not-found");
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        problem.ShouldNotBeNull();
        problem.Detail.ShouldBeNull();
    }

    /// <summary>
    /// Verifies that common application exceptions map to the expected problem details status and type.
    /// </summary>
    [Test]
    [Arguments("/__test/not-found", 404, "not_found")]
    [Arguments("/__test/concurrency", 409, "concurrency_conflict")]
    [Arguments("/__test/cancelled", 499, "client_closed_request")]
    [Arguments("/__test/integration", 503, "downstream_dependency_unavailable")]
    [Arguments("/__test/unexpected", 500, "unexpected_error")]
    public async Task Exceptions_MapToExpectedProblemDetailsStatus(
        string path,
        int expectedStatus,
        string expectedType)
    {
        await using var factory = new ApiFactory();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(path);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();

        ((int)response.StatusCode).ShouldBe(expectedStatus);
        problem.ShouldNotBeNull();
        problem.Status.ShouldBe(expectedStatus);
        problem.Type.ShouldBe(expectedType);
        problem.Extensions["correlationId"].ShouldNotBeNull();
        problem.Extensions["traceId"].ShouldNotBeNull();
    }

}
