using CeoAgent.Application.Abstractions.Secrets;
using CeoAgent.Infrastructure.ApiClient.WhatsApp;
using CeoAgent.Infrastructure.Implementation.Secrets;
using CeoAgent.Application.Abstractions.Messaging;
using CeoAgent.Shared.Messaging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Refit;

namespace CeoAgent.Infrastructure.Implementation.Messaging.WhatsApp;

/// <summary>
/// Implements WhatsApp Cloud messaging operations by resolving per-channel credentials and calling the Graph API.
/// </summary>
public sealed class WhatsAppCloudIntegration(
    IWhatsAppChannelCredentialResolver credentialResolver,
    IWhatsAppCloudClient client,
    ISecretValueProvider secrets,
    IConfiguration configuration,
    ILogger<WhatsAppCloudIntegration> logger) : IMessageChannelIntegration
{
    private const string DefaultGraphApiBaseUrl = "https://graph.facebook.com/v25.0";
    private const string MessagingProduct = "whatsapp";
    private static readonly EventId MessageSendStartingEvent = new(1001, "WhatsAppCloudMessageSendStarting");
    private static readonly EventId MessageSendFailedEvent = new(1002, "WhatsAppCloudMessageSendFailed");

    /// <summary>
    /// Marks a provider message as read through the WhatsApp Cloud messages endpoint.
    /// </summary>
    public async Task MarkMessageReadAsync(
        ChannelMessageReference message,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);

        var credential = await LoadCredentialAsync(message.CompanyChannelId, cancellationToken);
        await SendMessageAsync(
            credential: credential,
            new WhatsAppSendMessageRequest(
                MessagingProduct: MessagingProduct,
                RecipientType: null,
                To: null,
                Type: null,
                Text: null,
                Status: "read",
                MessageId: message.ProviderMessageId),
            companyId: message.CompanyId,
            companyChannelId: message.CompanyChannelId,
            conversationId: null,
            messageId: null,
            recipientExternalId: null,
            idempotencyKey: null,
            cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Sends a text reply to a WhatsApp recipient using the credential bound to the company channel.
    /// </summary>
    public async Task<SentMessageReference> SendTextAsync(
        ChannelTextMessage message,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);

        var credential = await LoadCredentialAsync(message.CompanyChannelId, cancellationToken);
        var response = await SendMessageAsync(
            credential: credential,
            new WhatsAppSendMessageRequest(
                MessagingProduct: MessagingProduct,
                RecipientType: "individual",
                To: message.RecipientExternalId,
                Type: "text",
                Text: new WhatsAppTextBody(PreviewUrl: false, Body: message.Text),
                Status: null,
                MessageId: null,
                BizOpaqueCallbackData: message.IdempotencyKey),
            companyId: message.CompanyId,
            companyChannelId: message.CompanyChannelId,
            conversationId: message.ConversationId,
            messageId: message.MessageId,
            recipientExternalId: message.RecipientExternalId,
            idempotencyKey: message.IdempotencyKey,
            cancellationToken: cancellationToken);

        return ToSentMessageReference(response);
    }

    private async Task<WhatsAppSendMessageResponse> SendMessageAsync(
        WhatsAppCredential credential,
        WhatsAppSendMessageRequest request,
        Guid companyId,
        Guid companyChannelId,
        Guid? conversationId,
        Guid? messageId,
        string? recipientExternalId,
        string? idempotencyKey,
        CancellationToken cancellationToken)
    {
        logger.LogInformation(
            MessageSendStartingEvent,
            "WhatsAppCloudMessageSendStarting CompanyId={CompanyId} CompanyChannelId={CompanyChannelId} ConversationId={ConversationId} MessageId={MessageId} Provider={Provider} MessageType={MessageType} Status={Status} HasIdempotencyKey={HasIdempotencyKey}",
            companyId,
            companyChannelId,
            conversationId,
            messageId,
            "whatsapp_cloud",
            request.Type,
            request.Status,
            !string.IsNullOrWhiteSpace(idempotencyKey));

        try
        {
            return await client.SendMessageAsync(
                credential.PhoneNumberId,
                credential.Authorization,
                request,
                cancellationToken);
        }
        catch (ApiException exception)
        {
            logger.LogWarning(
                MessageSendFailedEvent,
                exception,
                "WhatsAppCloudMessageSendFailed StatusCode={StatusCode} CompanyId={CompanyId} CompanyChannelId={CompanyChannelId} ConversationId={ConversationId} MessageId={MessageId} Provider={Provider} MessageType={MessageType} HasIdempotencyKey={HasIdempotencyKey}",
                (int)exception.StatusCode,
                companyId,
                companyChannelId,
                conversationId,
                messageId,
                "whatsapp_cloud",
                request.Type,
                !string.IsNullOrWhiteSpace(idempotencyKey));

            throw;
        }
    }

    private async Task<WhatsAppCredential> LoadCredentialAsync(Guid companyChannelId, CancellationToken cancellationToken)
    {
        var resolved = await credentialResolver.ResolveAsync(companyChannelId, cancellationToken);
        var credentialReference = resolved.CredentialReference;
        var accessToken = await secrets.GetSecretValueAsync(credentialReference, cancellationToken);

        return new WhatsAppCredential(
            resolved.PhoneNumberId,
            resolved.BusinessAccountId,
            credentialReference,
            accessToken);
    }

    private static SentMessageReference ToSentMessageReference(WhatsAppSendMessageResponse response)
    {
        var messageId = response.Messages is { Count: > 0 } messages
            ? messages[0].Id
            : null;
        if (string.IsNullOrWhiteSpace(messageId))
        {
            throw new InvalidOperationException("WhatsApp Cloud response did not include a sent message id.");
        }

        return new SentMessageReference(messageId);
    }

    private string BuildMessagesUrl(string phoneNumberId)
    {
        var baseUrl = configuration["WhatsApp:GraphApiBaseUrl"] ?? DefaultGraphApiBaseUrl;
        var normalizedBaseUrl = baseUrl.EndsWith('/')
            ? baseUrl
            : $"{baseUrl}/";
        return new Uri(new Uri(normalizedBaseUrl), $"{Uri.EscapeDataString(phoneNumberId)}/messages").ToString();
    }

    private sealed record WhatsAppCredential(
        string PhoneNumberId,
        string? BusinessAccountId,
        string CredentialReference,
        string AccessToken)
    {
        public string Authorization => $"Bearer {AccessToken}";
    }
}
