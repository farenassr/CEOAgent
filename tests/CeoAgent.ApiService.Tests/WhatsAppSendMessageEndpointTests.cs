using System.Net;
using System.Net.Http.Json;
using CeoAgent.ApiService.Tests.Support;
using CeoAgent.ApiService.Infrastructure.Security;
using CeoAgent.Integrations.Messaging;
using CeoAgent.Shared.Response.Company;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Shouldly;

namespace CeoAgent.ApiService.Tests;

[NotInParallel]
public sealed class WhatsAppSendMessageEndpointTests
{
    [Test]
    public async Task SendWhatsAppMessage_WithCompanyChannel_SendsThroughMessagingIntegration()
    {
        var messaging = new RecordingMessageChannelIntegration();
        const string adminKey = "test-admin-key";
        await using var factory = new ApiFactory(configureServices: services =>
        {
            services.RemoveAll<IMessageChannelIntegration>();
            services.AddSingleton<IMessageChannelIntegration>(messaging);
            services.Configure<AdminApiKeyOptions>(options =>
            {
                options.Key = adminKey;
            });
        });

        using var client = factory.CreateClient();
        var companyId = await CreateCompanyAsync(client, "Company A", adminKey);
        factory.Services.GetRequiredService<IOptions<AdminApiKeyOptions>>().Value.CompanyId = companyId;
        var credentialId = await RegisterWhatsAppCredentialAsync(client, companyId, adminKey);
        var channelId = await RegisterWhatsAppChannelAsync(client, companyId, credentialId, adminKey);

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"/v1/admin/companies/{companyId}/channels/{channelId}/whatsapp/messages")
        {
            Content = JsonContent.Create(new
            {
                recipientExternalId = "573001112233",
                text = "Hola desde CeoAgent",
                idempotencyKey = "manual-send-1",
            }),
        };
        request.Headers.Add("X-Admin-Api-Key", adminKey);

        using var response = await client.SendAsync(request);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<SendWhatsAppMessageResponse>();
        body.ShouldNotBeNull();
        body.ProviderMessageId.ShouldBe("wamid.sent-1");

        var sent = messaging.TextMessages.Single();
        sent.CompanyId.ShouldBe(companyId);
        sent.CompanyChannelId.ShouldBe(channelId);
        sent.RecipientExternalId.ShouldBe("573001112233");
        sent.Text.ShouldBe("Hola desde CeoAgent");
        sent.IdempotencyKey.ShouldBe("manual-send-1");
    }

    private static async Task<Guid> CreateCompanyAsync(HttpClient client, string name, string adminApiKey)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/v1/admin/companies")
        {
            Content = JsonContent.Create(new { name }),
        };
        request.Headers.Add("X-Admin-Api-Key", adminApiKey);

        using var response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<CompanyResponse>();
        body.ShouldNotBeNull();
        return body.Id;
    }

    private static async Task<Guid> RegisterWhatsAppCredentialAsync(HttpClient client, Guid companyId, string adminApiKey)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"/v1/admin/companies/{companyId}/integration-credentials")
        {
            Content = JsonContent.Create(new
            {
                provider = "whatsapp_cloud",
                purpose = "whatsapp_cloud",
                reference = "config://WhatsApp:AccessToken",
            }),
        };
        request.Headers.Add("X-Admin-Api-Key", adminApiKey);

        using var response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<IntegrationCredentialResponse>();
        body.ShouldNotBeNull();
        return body.Id;
    }

    private static async Task<Guid> RegisterWhatsAppChannelAsync(HttpClient client, Guid companyId, Guid credentialId, string adminApiKey)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/v1/admin/companies/{companyId}/channels")
        {
            Content = JsonContent.Create(new
            {
                provider = "whatsapp_cloud",
                providerChannelId = "1152556904604978",
                credentialReferenceId = credentialId,
                metadata = new
                {
                    whatsapp_cloud = new
                    {
                        business_account_id = "840790722416204",
                        phone_number_id = "1152556904604978",
                    },
                },
            }),
        };
        request.Headers.Add("X-Admin-Api-Key", adminApiKey);

        using var response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<CompanyChannelResponse>();
        body.ShouldNotBeNull();
        return body.Id;
    }

    private sealed class RecordingMessageChannelIntegration : IMessageChannelIntegration
    {
        public List<ChannelTextMessage> TextMessages { get; } = [];

        public Task MarkMessageReadAsync(ChannelMessageReference message, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task<DownloadedMedia> DownloadMediaAsync(ChannelMediaReference media, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task<SentMessageReference> SendTextAsync(ChannelTextMessage message, CancellationToken cancellationToken)
        {
            TextMessages.Add(message);
            return Task.FromResult(new SentMessageReference($"wamid.sent-{TextMessages.Count}"));
        }

        public Task<SentMessageReference> SendAudioAsync(ChannelAudioMessage message, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class SendWhatsAppMessageResponse
    {
        public string ProviderMessageId { get; set; } = string.Empty;
    }
}
