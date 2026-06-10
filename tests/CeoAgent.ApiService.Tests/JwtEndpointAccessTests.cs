using System.Net;
using System.Net.Http.Json;
using CeoAgent.ApiService.Tests.Support;
using Shouldly;

namespace CeoAgent.ApiService.Tests;

[NotInParallel]
public sealed class JwtEndpointAccessTests
{
    [Test]
    public async Task AdminEndpoint_WithBearerToken_DoesNotReturnUnauthorized()
    {
        await using var factory = new ApiFactory();
        using var client = factory.CreateAuthenticatedClient();

        using var response = await client.PostAsJsonAsync(
            "/v1/admin/companies",
            new
            {
                name = "Company A",
                timeZoneId = "America/Bogota",
            });

        response.StatusCode.ShouldNotBe(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task CompanyScopedEndpoint_UsesCompanyIdClaimAsTenantBoundary()
    {
        await using var factory = new ApiFactory();
        using var bootstrapClient = factory.CreateAuthenticatedClient();
        var companyId = await CreateCompanyAsync(bootstrapClient, "Company A");
        var otherCompanyId = await CreateCompanyAsync(bootstrapClient, "Company B");

        using var tenantAClient = factory.CreateAuthenticatedClient(companyId);
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/v1/admin/companies/{otherCompanyId}/channels")
        {
            Content = JsonContent.Create(new
            {
                provider = "whatsapp_cloud",
                providerChannelId = "123456",
            }),
        };

        using var response = await tenantAClient.SendAsync(request);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    private static async Task<Guid> CreateCompanyAsync(HttpClient client, string name)
    {
        using var response = await client.PostAsJsonAsync(
            "/v1/admin/companies",
            new
            {
                name,
                timeZoneId = "America/Bogota",
            });
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<CeoAgent.Shared.Response.Company.CompanyResponse>();
        body.ShouldNotBeNull();
        return body.Id;
    }
}
