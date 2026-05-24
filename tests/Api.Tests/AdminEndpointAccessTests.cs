using System.Net;
using System.Net.Http.Json;
using CEOAgent.Tests.Support;
using Shouldly;

namespace CEOAgent.Tests;

[NotInParallel]
public sealed class AdminEndpointAccessTests
{
    /// <summary>
    /// Verifies that admin endpoints allow requests without authentication.
    /// </summary>
    [Test]
    public async Task AdminEndpoint_WithoutAuthentication_AllowsRequest()
    {
        await using var factory = new ApiFactory();
        using var client = factory.CreateClient();

        using var response = await client.PostAsJsonAsync("/v1/admin/companies", new { name = "Company A" });

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
    }

    /// <summary>
    /// Verifies that company-scoped admin endpoints hide resources when the route company and header company differ.
    /// </summary>
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
                providerChannelId = "123456",
            }),
        };
        request.Headers.Add("X-Company-Id", otherCompanyId.ToString());

        using var response = await client.SendAsync(request);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    private static async Task<Guid> CreateCompanyAsync(HttpClient client, string name)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/v1/admin/companies")
        {
            Content = JsonContent.Create(new { name }),
        };

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
}
