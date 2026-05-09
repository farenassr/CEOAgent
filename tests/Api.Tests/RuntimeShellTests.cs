using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;

namespace CEOAgent.Tests;

public sealed class RuntimeShellTests
{
    [Fact]
    public async Task Health_ReturnsCorrelationIdHeader()
    {
        await using var factory = new ApiFactory();
        using var client = factory.CreateClient();

        using var request = new HttpRequestMessage(HttpMethod.Get, "/health");
        request.Headers.Add("X-Correlation-Id", "test-correlation-id");

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.TryGetValues("X-Correlation-Id", out var values));
        Assert.Equal("test-correlation-id", Assert.Single(values));
    }

    [Fact]
    public async Task BusinessRuleException_ReturnsProblemDetailsWithCorrelationExtension()
    {
        await using var factory = new ApiFactory();
        using var client = factory.CreateClient();

        using var request = new HttpRequestMessage(HttpMethod.Get, "/__test/business-rule");
        request.Headers.Add("X-Correlation-Id", "rule-correlation-id");

        using var response = await client.SendAsync(request);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();

        Assert.Equal((HttpStatusCode)422, response.StatusCode);
        Assert.NotNull(problem);
        Assert.Equal("Business rule violation", problem.Title);
        Assert.Equal("reservation_closed", problem.Extensions["code"]?.ToString());
        Assert.Equal("rule-correlation-id", problem.Extensions["correlationId"]?.ToString());
        Assert.NotNull(problem.Extensions["traceId"]);
    }

    [Theory]
    [InlineData("/__test/not-found", 404)]
    [InlineData("/__test/concurrency", 409)]
    [InlineData("/__test/cancelled", 499)]
    [InlineData("/__test/integration", 503)]
    [InlineData("/__test/unexpected", 500)]
    public async Task Exceptions_MapToExpectedProblemDetailsStatus(string path, int expectedStatus)
    {
        await using var factory = new ApiFactory();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(path);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();

        Assert.Equal(expectedStatus, (int)response.StatusCode);
        Assert.NotNull(problem);
        Assert.Equal(expectedStatus, problem.Status);
        Assert.NotNull(problem.Extensions["correlationId"]);
        Assert.NotNull(problem.Extensions["traceId"]);
    }

    private sealed class ApiFactory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
        }
    }
}
