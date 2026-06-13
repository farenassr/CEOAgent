using System.Net;
using System.Net.Http.Json;
using CeoAgent.ApiService.Tests.Support;
using CeoAgent.Application.Abstractions.Messaging;
using CeoAgent.Shared.Messaging;
using CeoAgent.Shared.Response.Company;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Shouldly;

namespace CeoAgent.ApiService.Tests;

[NotInParallel]
public sealed class WhatsAppSendMessageEndpointTests
{
    [Test]
    public async Task SendWhatsAppMessage_WithCompanyChannel_SendsThroughMessagingIntegration()
    {
        var messaging = new RecordingMessageChannelIntegration();
        await using var factory = new ApiFactory(configureServices: services =>
        {
            services.RemoveAll<IMessageChannelIntegration>();
            services.AddSingleton<IMessageChannelIntegration>(messaging);
        });

        using var bootstrapClient = factory.CreateAuthenticatedClient();
        var organizationId = await CreateCompanyAsync(bootstrapClient, "Organization A");
        using var tenantClient = factory.CreateAuthenticatedClient(organizationId);
        var credentialId = await RegisterWhatsAppCredentialAsync(tenantClient);
        var channelId = await RegisterWhatsAppChannelAsync(tenantClient, credentialId);

        using var response = await tenantClient.PostAsJsonAsync(
            $"/v1/admin/channels/{channelId}/whatsapp/messages",
            new
            {
                recipientExternalId = "573001112233",
                text = "Hola desde CeoAgent",
                idempotencyKey = "manual-send-1",
            });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<SendWhatsAppMessageResponse>();
        body.ShouldNotBeNull();
        body.ProviderMessageId.ShouldBe("wamid.sent-1");

        var sent = messaging.TextMessages.Single();
        sent.OrganizationId.ShouldBe(organizationId);
        sent.CompanyChannelId.ShouldBe(channelId);
        sent.RecipientExternalId.ShouldBe("573001112233");
        sent.Text.ShouldBe("Hola desde CeoAgent");
        sent.IdempotencyKey.ShouldBe("manual-send-1");
    }

    [Test]
    public async Task SendWhatsAppMessage_WhenRecipientExternalIdIncludesPlus_ReturnsBadRequestWithoutSending()
    {
        var messaging = new RecordingMessageChannelIntegration();
        await using var factory = new ApiFactory(configureServices: services =>
        {
            services.RemoveAll<IMessageChannelIntegration>();
            services.AddSingleton<IMessageChannelIntegration>(messaging);
        });

        using var bootstrapClient = factory.CreateAuthenticatedClient();
        var organizationId = await CreateCompanyAsync(bootstrapClient, "Organization A");
        using var tenantClient = factory.CreateAuthenticatedClient(organizationId);
        var credentialId = await RegisterWhatsAppCredentialAsync(tenantClient);
        var channelId = await RegisterWhatsAppChannelAsync(tenantClient, credentialId);

        using var response = await tenantClient.PostAsJsonAsync(
            $"/v1/admin/channels/{channelId}/whatsapp/messages",
            new
            {
                recipientExternalId = "+971529596724",
                text = "Hola!",
                idempotencyKey = (string?)null,
            });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        messaging.TextMessages.ShouldBeEmpty();
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

    private static async Task<Guid> RegisterWhatsAppCredentialAsync(HttpClient client)
    {
        using var response = await client.PostAsJsonAsync(
            "/v1/admin/integration-credentials",
            new
            {
                provider = "whatsapp_cloud",
                purpose = "whatsapp_cloud",
                reference = "config://WhatsApp:AccessToken",
            });
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<IntegrationCredentialResponse>();
        body.ShouldNotBeNull();
        return body.Id;
    }

    private static async Task<Guid> RegisterWhatsAppChannelAsync(HttpClient client, Guid credentialId)
    {
        using var response = await client.PostAsJsonAsync(
            "/v1/admin/channels",
            new
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
            });
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

        public Task<SentMessageReference> SendTextAsync(ChannelTextMessage message, CancellationToken cancellationToken)
        {
            TextMessages.Add(message);
            return Task.FromResult(new SentMessageReference($"wamid.sent-{TextMessages.Count}"));
        }
    }

    private sealed class SendWhatsAppMessageResponse
    {
        public string ProviderMessageId { get; set; } = string.Empty;
    }
}
