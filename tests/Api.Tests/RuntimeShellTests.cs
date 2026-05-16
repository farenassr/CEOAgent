using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Shouldly;

namespace CEOAgent.Tests;

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
        problem.Extensions["code"]?.ToString().ShouldBe("reservation_closed");
        problem.Extensions["correlationId"]?.ToString().ShouldBe("rule-correlation-id");
        problem.Extensions["traceId"].ShouldNotBeNull();
    }

    [Test]
    [Arguments("/__test/not-found", 404)]
    [Arguments("/__test/concurrency", 409)]
    [Arguments("/__test/cancelled", 499)]
    [Arguments("/__test/integration", 503)]
    [Arguments("/__test/unexpected", 500)]
    public async Task Exceptions_MapToExpectedProblemDetailsStatus(string path, int expectedStatus)
    {
        await using var factory = new ApiFactory();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(path);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();

        ((int)response.StatusCode).ShouldBe(expectedStatus);
        problem.ShouldNotBeNull();
        problem.Status.ShouldBe(expectedStatus);
        problem.Extensions["correlationId"].ShouldNotBeNull();
        problem.Extensions["traceId"].ShouldNotBeNull();
    }

    private sealed class ApiFactory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
        }
    }
}
