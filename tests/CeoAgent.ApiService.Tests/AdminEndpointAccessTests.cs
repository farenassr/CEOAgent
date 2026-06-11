using System.Net;
using System.Net.Http.Json;
using CeoAgent.ApiService.Tests.Support;
using CeoAgent.Shared.Response.Company;
using Shouldly;

namespace CeoAgent.ApiService.Tests;

[NotInParallel]
public sealed class AdminEndpointAccessTests
{
    [Test]
    public async Task AdminEndpoint_WithoutBearerToken_ReturnsUnauthorized()
    {
        await using var factory = new ApiFactory();
        using var client = factory.CreateClient();

        using var response = await client.PostAsJsonAsync(
            "/v1/admin/companies",
            new
            {
                name = "Organization A",
                timeZoneId = "America/Bogota",
            });

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

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
    public async Task CompanyScopedEndpoint_WhenRouteCompanyDiffersFromJwtOrganization_Returns404()
    {
        await using var factory = new ApiFactory();
        var organizationAId = Guid.CreateVersion7();
        var organizationBId = Guid.CreateVersion7();
        using var organizationAClient = factory.CreateAuthenticatedClient(organizationAId);
        using var organizationBClient = factory.CreateAuthenticatedClient(organizationBId);
        organizationAId = await CreateCompanyAsync(organizationAClient, "Organization A");
        organizationBId = await CreateCompanyAsync(organizationBClient, "Organization B");

        using var organizationScopedClient = factory.CreateAuthenticatedClient(organizationAId);
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/v1/admin/companies/{organizationBId}/channels")
        {
            Content = JsonContent.Create(new
            {
                provider = "whatsapp_cloud",
                providerChannelId = "tenant-b-channel-denied",
                metadata = new
                {
                    whatsapp_cloud = new
                    {
                        business_account_id = "987654321",
                        phone_number_id = "tenant-b-channel-denied",
                    },
                },
            }),
        };

        using var response = await organizationScopedClient.SendAsync(request);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task RegisterCompanyChannel_WithWhatsAppCloudProvider_ReturnsCompanyChannelResponse()
    {
        await using var factory = new ApiFactory();
        using var bootstrapClient = factory.CreateAuthenticatedClient();
        var organizationId = await CreateCompanyAsync(bootstrapClient, "Organization A");

        using var tenantClient = factory.CreateAuthenticatedClient(organizationId);
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/v1/admin/companies/{organizationId}/channels")
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

        using var response = await tenantClient.SendAsync(request);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<CompanyChannelResponse>();
        body.ShouldNotBeNull();
        body.Id.ShouldNotBe(Guid.Empty);
        body.OrganizationId.ShouldBe(organizationId);
        body.ProviderChannelId.ShouldBe("123456");
        body.Metadata.ShouldNotBeNull();
        body.Metadata.Value.GetProperty("whatsapp_cloud").GetProperty("phone_number_id").GetString().ShouldBe("123456");
    }

    [Test]
    public async Task RegisterIntegrationCredential_WhenMetadataContainsCredentialMaterial_ReturnsBadRequest()
    {
        await using var factory = new ApiFactory();
        using var bootstrapClient = factory.CreateAuthenticatedClient();
        var organizationId = await CreateCompanyAsync(bootstrapClient, "Organization A");

        using var tenantClient = factory.CreateAuthenticatedClient(organizationId);
        using var request = CreateCredentialRequest(
            organizationId,
            "kv://google-calendar/contoso/service-account",
            new
            {
                provider = "google_calendar",
                google_calendar = new
                {
                    calendarId = "primary",
                    private_key = "-----BEGIN PRIVATE KEY-----\\nxxx\\n-----END PRIVATE KEY-----\\n",
                },
            });

        using var response = await tenantClient.SendAsync(request);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Arguments("plain-token-value")]
    [Arguments("Bearer abc123")]
    [Arguments("stored://google-calendar/contoso")]
    [Test]
    public async Task RegisterIntegrationCredential_WhenReferenceIsNotSupportedSecretReference_ReturnsBadRequest(string reference)
    {
        await using var factory = new ApiFactory();
        using var bootstrapClient = factory.CreateAuthenticatedClient();
        var organizationId = await CreateCompanyAsync(bootstrapClient, "Organization A");

        using var tenantClient = factory.CreateAuthenticatedClient(organizationId);
        using var request = CreateCredentialRequest(organizationId, reference);

        using var response = await tenantClient.SendAsync(request);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Arguments("kv://google-calendar/contoso/service-account")]
    [Arguments("config://Secrets:GoogleCalendar:Contoso")]
    [Arguments("https://contoso.vault.azure.net/secrets/google-calendar-service-account")]
    [Test]
    public async Task RegisterIntegrationCredential_WhenReferenceIsSupportedSecretReference_ReturnsOk(string reference)
    {
        await using var factory = new ApiFactory();
        using var bootstrapClient = factory.CreateAuthenticatedClient();
        var organizationId = await CreateCompanyAsync(bootstrapClient, "Organization A");

        using var tenantClient = factory.CreateAuthenticatedClient(organizationId);
        using var request = CreateCredentialRequest(organizationId, reference);

        using var response = await tenantClient.SendAsync(request);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    private static HttpRequestMessage CreateCredentialRequest(Guid organizationId, string reference, object? metadata = null)
    {
        return new HttpRequestMessage(HttpMethod.Post, $"/v1/admin/companies/{organizationId}/integration-credentials")
        {
            Content = JsonContent.Create(new
            {
                provider = "google_calendar",
                purpose = "calendar",
                reference,
                metadata,
            }),
        };
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
        var body = await response.Content.ReadFromJsonAsync<CompanyResponse>();
        body.ShouldNotBeNull();
        return body.Id;
    }
}
