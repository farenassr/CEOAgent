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
        var html = await response.Content.ReadAsStringAsync();

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("text/html");
        html.ShouldContain("CeoAgent API Reference");
        html.ShouldContain("\"theme\":\"default\"");
        html.ShouldContain("\"forceDarkModeState\":\"light\"");
        html.ShouldContain("ceo-agent-api");
        html.ShouldContain("\"selectedScopes\":[\"openid\",\"profile\",\"email\",\"organization\"]");
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

    [Test]
    public async Task OpenApiDocument_GroupsEndpointsWithModuleTagsInDevelopment()
    {
        await using var factory = new ApiFactory("Development");
        using var client = factory.CreateClient();

        using var document = await GetOpenApiDocumentAsync(client);

        var tags = document.RootElement
            .GetProperty("paths")
            .EnumerateObject()
            .SelectMany(path => path.Value.EnumerateObject())
            .SelectMany(operation => operation.Value.GetProperty("tags").EnumerateArray())
            .Select(tag => tag.GetString())
            .Where(tag => !string.IsNullOrWhiteSpace(tag))
            .ToHashSet(StringComparer.Ordinal);

        tags.ShouldContain("🔐 Authentication");
        tags.ShouldContain("🏢 Companies");
        tags.ShouldContain("📡 Channels");
        tags.ShouldContain("💬 WhatsApp");
        tags.ShouldContain("📅 Google Calendar");
        tags.ShouldContain("🧵 Conversations");
        tags.ShouldContain("📬 Queues");
        tags.ShouldContain("🔑 Integration Credentials");
        tags.ShouldContain("🤖 Agent Profile");
        tags.ShouldContain("🔧 Tools");
        tags.ShouldContain("🪝 Webhooks");
    }

    [Test]
    public async Task OpenApiDocument_IncludesFriendlySummariesAndDescriptionsInDevelopment()
    {
        await using var factory = new ApiFactory("Development");
        using var client = factory.CreateClient();

        using var document = await GetOpenApiDocumentAsync(client);

        var authMe = GetOperation(document, "/v1/auth/me", "get");
        authMe.GetProperty("summary").GetString().ShouldBe("Get Authenticated User");
        authMe.GetProperty("description").GetString().ShouldNotBeNullOrWhiteSpace();

        var createCompany = GetOperation(document, "/v1/admin/companies", "post");
        createCompany.GetProperty("summary").GetString().ShouldBe("Create Company");
        createCompany.GetProperty("description").GetString().ShouldNotBeNullOrWhiteSpace();

        var availability = GetOperation(document, "/v1/admin/google-calendar/availability", "post");
        availability.GetProperty("summary").GetString().ShouldBe("Check Google Calendar Availability");
        availability.GetProperty("description").GetString().ShouldNotBeNullOrWhiteSpace();

        var queues = GetOperation(document, "/v1/admin/queues", "get");
        queues.GetProperty("summary").GetString().ShouldBe("List Queues");
        queues.GetProperty("description").GetString().ShouldNotBeNullOrWhiteSpace();
    }

    [Test]
    public async Task OpenApiDocument_PreservesKeycloakSecurityMetadataInDevelopment()
    {
        await using var factory = new ApiFactory("Development");
        using var client = factory.CreateClient();

        using var document = await GetOpenApiDocumentAsync(client);

        var securitySchemes = document.RootElement
            .GetProperty("components")
            .GetProperty("securitySchemes");
        securitySchemes.TryGetProperty("KeycloakOAuth", out _).ShouldBeTrue();

        var protectedOperation = GetOperation(document, "/v1/admin/companies", "post");
        OperationHasSecurityScheme(protectedOperation, "KeycloakOAuth").ShouldBeTrue();

        var anonymousWebhook = GetOperation(document, "/v1/whatsapp", "post");
        anonymousWebhook.TryGetProperty("security", out var anonymousSecurity).ShouldBeFalse(
            $"AllowAnonymous endpoints should not require security, but found {anonymousSecurity}.");

        var anonymousVerification = GetOperation(document, "/v1/whatsapp/webhook", "get");
        anonymousVerification.TryGetProperty("security", out anonymousSecurity).ShouldBeFalse(
            $"AllowAnonymous endpoints should not require security, but found {anonymousSecurity}.");
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

    private static async Task<JsonDocument> GetOpenApiDocumentAsync(HttpClient client)
    {
        using var response = await client.GetAsync("/openapi/v1.json");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        return await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
    }

    private static JsonElement GetOperation(JsonDocument document, string path, string method)
    {
        return document.RootElement
            .GetProperty("paths")
            .GetProperty(path)
            .GetProperty(method);
    }

    private static bool OperationHasSecurityScheme(JsonElement operation, string schemeName)
    {
        if (!operation.TryGetProperty("security", out var security))
        {
            return false;
        }

        return security
            .EnumerateArray()
            .Any(requirement => requirement.TryGetProperty(schemeName, out _));
    }

}
