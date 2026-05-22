using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Shouldly;

namespace CEOAgent.Tests;

[NotInParallel]
public sealed class RuntimeShellTests
{
    [Test]
    public async Task Health_ReturnsCorrelationIdHeader()
    {
        await using var factory = new ApiFactory();
        using var client = factory.CreateClient();

        using var request = new HttpRequestMessage(HttpMethod.Get, "/health");
        request.Headers.Add("X-Correlation-Id", "test-correlation-id");

        using var response = await client.SendAsync(request);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Headers.TryGetValues("X-Correlation-Id", out var values).ShouldBeTrue();
        values.Single().ShouldBe("test-correlation-id");
    }

    [Test]
    public async Task Health_GeneratesCorrelationIdHeaderWhenMissing()
    {
        await using var factory = new ApiFactory();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/health");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Headers.TryGetValues("X-Correlation-Id", out var values).ShouldBeTrue();
        Guid.TryParse(values.Single(), out _).ShouldBeTrue();
    }

    [Test]
    public async Task ScalarApiReference_IsAvailableAtApiInDevelopment()
    {
        await using var factory = new ApiFactory("Development");
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/api");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("text/html");
    }

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
            .TryGetProperty("/health", out _)
            .ShouldBeTrue();
    }

    [Test]
    public async Task BusinessRuleException_ReturnsProblemDetailsWithCorrelationExtension()
    {
        await using var factory = new ApiFactory();
        using var client = factory.CreateClient();

        using var request = new HttpRequestMessage(HttpMethod.Get, "/__test/business-rule");
        request.Headers.Add("X-Correlation-Id", "rule-correlation-id");

        using var response = await client.SendAsync(request);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();

        response.StatusCode.ShouldBe((HttpStatusCode)422);
        problem.ShouldNotBeNull();
        problem.Title.ShouldBe("Business rule violation");
        problem.Type.ShouldBe("business_rule_violation");
        problem.Extensions["code"]?.ToString().ShouldBe("conversation_closed");
        problem.Extensions["correlationId"]?.ToString().ShouldBe("rule-correlation-id");
        problem.Extensions["traceId"].ShouldNotBeNull();
    }

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

    private sealed class ApiFactory(string environmentName = "Testing") : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
        {
            builder.UseEnvironment(environmentName);
            builder.UseSetting("Authentication:AdminApiKey", "test-admin-key");
            builder.UseSetting("Persistence:UseInMemoryDatabase", "true");
            builder.UseSetting("Persistence:InMemoryDatabaseName", $"runtime-shell-tests-{Guid.CreateVersion7()}");
        }
    }
}
