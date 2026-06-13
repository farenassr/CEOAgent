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
                name = "Organization A",
                timeZoneId = "America/Bogota",
            });

        response.StatusCode.ShouldNotBe(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task CompanyScopedEndpoint_UsesOrganizationIdClaimWithoutRouteOrganization()
    {
        await using var factory = new ApiFactory();
        var organizationId = Guid.CreateVersion7();
        var otherOrganizationId = Guid.CreateVersion7();
        using var organizationClient = factory.CreateAuthenticatedClient(organizationId);
        using var otherOrganizationClient = factory.CreateAuthenticatedClient(otherOrganizationId);
        organizationId = await CreateCompanyAsync(organizationClient, "Organization A");
        _ = await CreateCompanyAsync(otherOrganizationClient, "Organization B");

        using var tenantAClient = factory.CreateAuthenticatedClient(organizationId);
        using var request = new HttpRequestMessage(HttpMethod.Post, "/v1/admin/channels")
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

        using var response = await tenantAClient.SendAsync(request);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
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
