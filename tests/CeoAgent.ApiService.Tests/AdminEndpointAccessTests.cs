using System;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using CeoAgent.ApiService.Infrastructure.Security;
using CeoAgent.ApiService.Tests.Support;
using CeoAgent.Shared.Response.Company;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Shouldly;

namespace CeoAgent.ApiService.Tests;

[NotInParallel]
public sealed class AdminEndpointAccessTests
{
    [Test]
    public async Task AdminEndpoint_WithoutApiKey_ReturnsUnauthorized()
    {
        await using var factory = new ApiFactory();

        using var client = factory.CreateClient();

        using var response = await client.PostAsJsonAsync("/v1/admin/companies", new { name = "Company A" });

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task AdminEndpoint_WithInvalidApiKey_ReturnsUnauthorized()
    {
        await using var factory = new ApiFactory(configureServices: services =>
        {
            services.Configure<AdminApiKeyOptions>(options =>
            {
                options.Key = "valid-key";
                options.CompanyId = Guid.NewGuid();
            });
        });

        using var client = factory.CreateClient();

        using var request = new HttpRequestMessage(HttpMethod.Post, "/v1/admin/companies")
        {
            Content = JsonContent.Create(new { name = "Company A" })
        };
        request.Headers.Add("X-Admin-Api-Key", "invalid-key");

        using var response = await client.SendAsync(request);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task AdminEndpoint_WithValidApiKey_DoesNotReturnUnauthorized()
    {
        var companyId = Guid.NewGuid();
        await using var factory = new ApiFactory(configureServices: services =>
        {
            services.Configure<AdminApiKeyOptions>(options =>
            {
                options.Key = "valid-key";
                options.CompanyId = companyId;
            });
        });

        using var client = factory.CreateClient();

        using var request = new HttpRequestMessage(HttpMethod.Post, "/v1/admin/companies")
        {
            Content = JsonContent.Create(new { name = "Company A" })
        };
        request.Headers.Add("X-Admin-Api-Key", "valid-key");

        using var response = await client.SendAsync(request);

        response.StatusCode.ShouldNotBe(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task AdminEndpoint_XCompanyIdHeader_DoesNotOverwriteAdminTenant()
    {
        var companyId = Guid.NewGuid();
        await using var factory = new ApiFactory(configureServices: services =>
        {
            services.Configure<AdminApiKeyOptions>(options =>
            {
                options.Key = "valid-key";
                options.CompanyId = companyId;
            });
        });

        using var client = factory.CreateClient();

        using var request = new HttpRequestMessage(HttpMethod.Post, "/v1/admin/companies")
        {
            Content = JsonContent.Create(new { name = "Company A" })
        };
        request.Headers.Add("X-Admin-Api-Key", "valid-key");
        request.Headers.Add("X-Company-Id", Guid.NewGuid().ToString());

        using var response = await client.SendAsync(request);
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
    }

    [Test]
    public async Task CompanyScopedEndpoint_WhenRouteCompanyDiffersFromHeaderCompany_Returns404()
    {
        var adminKey = "test-admin-key";
        await using var factory = new ApiFactory(configureServices: services =>
        {
            services.Configure<AdminApiKeyOptions>(options =>
            {
                options.Key = adminKey;
            });
        });

        using var client = factory.CreateClient();
        var companyId = await CreateCompanyAsync(client, "Company A", adminKey);
        var otherCompanyId = await CreateCompanyAsync(client, "Company B", adminKey);

        var adminOptions = factory.Services.GetRequiredService<IOptions<AdminApiKeyOptions>>().Value;
        adminOptions.CompanyId = otherCompanyId;

        using var request = new HttpRequestMessage(HttpMethod.Post, $"/v1/admin/companies/{companyId}/channels")
        {
            Content = JsonContent.Create(new
            {
                provider = "whatsapp_cloud",
                providerChannelId = "123456",
            }),
        };
        request.Headers.Add("X-Admin-Api-Key", adminKey);
        request.Headers.Add("X-Company-Id", companyId.ToString());

        using var response = await client.SendAsync(request);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task RegisterCompanyChannel_WithWhatsAppCloudProvider_ReturnsCompanyChannelResponse()
    {
        var adminKey = "test-admin-key";
        await using var factory = new ApiFactory(configureServices: services =>
        {
            services.Configure<AdminApiKeyOptions>(options =>
            {
                options.Key = adminKey;
            });
        });

        using var client = factory.CreateClient();
        var companyId = await CreateCompanyAsync(client, "Company A", adminKey);

        var adminOptions = factory.Services.GetRequiredService<IOptions<AdminApiKeyOptions>>().Value;
        adminOptions.CompanyId = companyId;

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
        request.Headers.Add("X-Admin-Api-Key", adminKey);

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

    private static async Task<Guid> CreateCompanyAsync(HttpClient client, string name, string? apiKey = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/v1/admin/companies")
        {
            Content = JsonContent.Create(new { name }),
        };

        if (apiKey is not null)
        {
            request.Headers.Add("X-Admin-Api-Key", apiKey);
        }

        using var response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<CompanyResponse>();
        body.ShouldNotBeNull();
        return body.Id;
    }
}
