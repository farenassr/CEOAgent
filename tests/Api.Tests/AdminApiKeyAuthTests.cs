using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Shouldly;

namespace CEOAgent.Tests;

[NotInParallel]
public sealed class AdminApiKeyAuthTests
{
    [Test]
    public async Task AdminEndpoint_MissingApiKey_Returns401()
    {
        await using var factory = new ApiFactory();
        using var client = factory.CreateClient();

        using var response = await client.PostAsJsonAsync("/v1/admin/companies", new { name = "Company A" });

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task AdminEndpoint_InvalidApiKey_Returns401()
    {
        await using var factory = new ApiFactory();
        using var client = factory.CreateClient();

        using var request = new HttpRequestMessage(HttpMethod.Post, "/v1/admin/companies")
        {
            Content = JsonContent.Create(new { name = "Company A" })
        };
        request.Headers.Add("X-Admin-Api-Key", "wrong");

        using var response = await client.SendAsync(request);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task AdminEndpoint_ValidApiKey_AllowsRequest()
    {
        await using var factory = new ApiFactory();
        using var client = factory.CreateClient();

        using var request = new HttpRequestMessage(HttpMethod.Post, "/v1/admin/companies")
        {
            Content = JsonContent.Create(new { name = "Company A" })
        };
        request.Headers.Add("X-Admin-Api-Key", "test-admin-key");

        using var response = await client.SendAsync(request);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
    }

    [Test]
    public async Task CompanyScopedEndpoint_WhenRouteCompanyDiffersFromHeaderCompany_Returns404()
    {
        await using var factory = new ApiFactory();
        using var client = factory.CreateClient();
        var companyId = await CreateCompanyAsync(client, "Company A");
        var otherCompanyId = await CreateCompanyAsync(client, "Company B");

        using var request = new HttpRequestMessage(HttpMethod.Post, $"/v1/admin/companies/{companyId}/channels")
        {
            Content = JsonContent.Create(new
            {
                provider = "whatsapp_cloud",
                providerChannelId = "123456"
            })
        };
        request.Headers.Add("X-Admin-Api-Key", "test-admin-key");
        request.Headers.Add("X-Company-Id", otherCompanyId.ToString());

        using var response = await client.SendAsync(request);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    private static async Task<Guid> CreateCompanyAsync(HttpClient client, string name)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/v1/admin/companies")
        {
            Content = JsonContent.Create(new { name })
        };
        request.Headers.Add("X-Admin-Api-Key", "test-admin-key");

        using var response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<CompanyResponse>();
        body.ShouldNotBeNull();
        return body.Id;
    }

    private sealed class CompanyResponse
    {
        public Guid Id { get; set; }

        public string Name { get; set; } = string.Empty;

        public string Status { get; set; } = string.Empty;
    }

    private sealed class ApiFactory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.UseSetting("Authentication:AdminApiKey", "test-admin-key");
            builder.UseSetting("Persistence:UseInMemoryDatabase", "true");
            builder.UseSetting("Persistence:InMemoryDatabaseName", $"admin-api-key-tests-{Guid.CreateVersion7()}");
        }
    }
}
