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

    [Test]
    public async Task OpenApiDocument_PaymentAccountMultipartForm_OnlyQrImageIsBinaryAndAccountTypeIsEnum()
    {
        await using var factory = new ApiFactory("Development");
        using var client = factory.CreateClient();

        using var document = await GetOpenApiDocumentAsync(client);

        var operation = GetOperation(document, "/v1/admin/payment-accounts", "post");
        var properties = GetMultipartFormProperties(document, operation);
        properties.TryGetProperty("qrImage", out var qrImage).ShouldBeTrue();
        qrImage.GetProperty("type").GetString().ShouldBe("string");
        qrImage.GetProperty("format").GetString().ShouldBe("binary");

        var binaryProperties = properties
            .EnumerateObject()
            .Where(property => property.Value.TryGetProperty("format", out var format)
                && string.Equals(format.GetString(), "binary", StringComparison.Ordinal))
            .Select(property => property.Name)
            .ToArray();
        binaryProperties.ShouldBe(["qrImage"]);

        properties.TryGetProperty("accountType", out var accountType).ShouldBeTrue();
        accountType.GetProperty("type").GetString().ShouldBe("string");
        accountType.GetProperty("enum")
            .EnumerateArray()
            .Select(value => value.GetString())
            .ShouldBe(["Ahorros", "Corriente"]);
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

    private static JsonElement GetMultipartFormProperties(JsonDocument document, JsonElement operation)
    {
        var schema = operation
            .GetProperty("requestBody")
            .GetProperty("content")
            .GetProperty("multipart/form-data")
            .GetProperty("schema");
        return ResolveSchema(document, schema).GetProperty("properties");
    }

    private static JsonElement ResolveSchema(JsonDocument document, JsonElement schema)
    {
        if (!schema.TryGetProperty("$ref", out var schemaReference))
        {
            return schema;
        }

        var reference = schemaReference.GetString();
        reference.ShouldNotBeNull();
        var parts = reference.Split('/', StringSplitOptions.RemoveEmptyEntries);
        parts.ShouldBe(["#", "components", "schemas", parts[^1]]);
        return document.RootElement
            .GetProperty("components")
            .GetProperty("schemas")
            .GetProperty(parts[^1]);
    }

}
