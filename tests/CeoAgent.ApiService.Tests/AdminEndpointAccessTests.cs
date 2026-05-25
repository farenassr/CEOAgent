using System.Net;
using System.Net.Http.Json;
using CeoAgent.Shared.Response.Company;
using CeoAgent.ApiService.Tests.Support;
using Shouldly;

namespace CeoAgent.ApiService.Tests;

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

    [Test]
    public async Task RegisterCompanyChannel_WithWhatsAppCloudProvider_ReturnsCompanyChannelResponse()
    {
        await using var factory = new ApiFactory();
        using var client = factory.CreateClient();
        var companyId = await CreateCompanyAsync(client, "Company A");

        using var request = new HttpRequestMessage(HttpMethod.Post, $"/v1/admin/companies/{companyId}/channels")
        {
            Content = JsonContent.Create(new
            {
                provider = "whatsapp_cloud",
                providerChannelId = "123456",
                metadata = new
                {
                    whatsapp_cloud = new
                    {
                        business_account_id = "987654321",
                        phone_number_id = "123456",
                    },
                },
            }),
        };
        request.Headers.Add("X-Company-Id", companyId.ToString());

        using var response = await client.SendAsync(request);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<CompanyChannelResponse>();
        body.ShouldNotBeNull();
        body.Id.ShouldNotBe(Guid.Empty);
        body.CompanyId.ShouldBe(companyId);
        body.ProviderChannelId.ShouldBe("123456");
        body.Metadata.ShouldNotBeNull();
        body.Metadata.Value.GetProperty("whatsapp_cloud").GetProperty("phone_number_id").GetString().ShouldBe("123456");
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
}
