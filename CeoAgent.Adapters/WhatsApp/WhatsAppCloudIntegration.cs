using CeoAgent.Adapters.WhatsApp.Abstractions;
using CeoAgent.Adapters.WhatsApp.Client;
using CeoAgent.Integrations.Messaging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Refit;
using System.Security.Cryptography;

namespace CeoAgent.Adapters.WhatsApp;

/// <summary>
/// Implements WhatsApp Cloud messaging operations by resolving per-channel credentials and calling the Graph API.
/// </summary>
public sealed class WhatsAppCloudIntegration(
    IWhatsAppChannelCredentialResolver credentialResolver,
    IWhatsAppCloudRefitClient client,
    ISecretValueProvider secrets,
    HttpClient mediaHttpClient,
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
                Audio: null,
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
    /// Downloads WhatsApp media by first resolving provider metadata and then streaming the protected media URL.
    /// </summary>
    public async Task<DownloadedMedia> DownloadMediaAsync(
        ChannelMediaReference media,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(media);

        var credential = await LoadCredentialAsync(media.CompanyChannelId, cancellationToken);
        var metadata = await client.GetMediaAsync(media.ProviderMediaId, credential.Authorization, cancellationToken);
        using var request = new HttpRequestMessage(HttpMethod.Get, metadata.Url);
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", credential.AccessToken);

        using var response = await mediaHttpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        var content = new MemoryStream();
        await response.Content.CopyToAsync(content, cancellationToken);
        content.Position = 0;
        var contentType = response.Content.Headers.ContentType?.MediaType
            ?? metadata.MimeType
            ?? "application/octet-stream";

        return new DownloadedMedia(
            content,
            contentType,
            ExtensionFromContentType(contentType),
            metadata.FileSize ?? content.Length);
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
                Audio: null,
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

    /// <summary>
    /// Sends an audio reply to a WhatsApp recipient using a publicly reachable media URI.
    /// </summary>
    public async Task<SentMessageReference> SendAudioAsync(
        ChannelAudioMessage message,
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
                Type: "audio",
                Text: null,
                Audio: new WhatsAppMediaBody(Link: message.AudioUri.ToString()),
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
        var url = BuildMessagesUrl(credential.PhoneNumberId);
        logger.LogInformation(
            MessageSendStartingEvent,
            "WhatsAppCloudMessageSendStarting Url={Url} CompanyId={CompanyId} CompanyChannelId={CompanyChannelId} ConversationId={ConversationId} MessageId={MessageId} PhoneNumberId={PhoneNumberId} BusinessAccountId={BusinessAccountId} RecipientExternalId={RecipientExternalId} IdempotencyKey={IdempotencyKey} CredentialReference={CredentialReference} AccessTokenLength={AccessTokenLength} AccessTokenSha256Prefix={AccessTokenSha256Prefix} MessagingProduct={MessagingProduct} RecipientType={RecipientType} MessageType={MessageType} TextLength={TextLength} AudioLink={AudioLink} Status={Status} ProviderMessageId={ProviderMessageId} BizOpaqueCallbackData={BizOpaqueCallbackData}",
            url,
            companyId,
            companyChannelId,
            conversationId,
            messageId,
            credential.PhoneNumberId,
            credential.BusinessAccountId,
            recipientExternalId ?? request.To,
            idempotencyKey,
            credential.CredentialReference,
            credential.AccessToken.Length,
            HashPrefix(credential.AccessToken),
            request.MessagingProduct,
            request.RecipientType,
            request.Type,
            request.Text?.Body.Length,
            request.Audio?.Link,
            request.Status,
            request.MessageId,
            request.BizOpaqueCallbackData);

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
                "WhatsAppCloudMessageSendFailed Url={Url} StatusCode={StatusCode} ResponseContent={ResponseContent} CompanyId={CompanyId} CompanyChannelId={CompanyChannelId} PhoneNumberId={PhoneNumberId} BusinessAccountId={BusinessAccountId} RecipientExternalId={RecipientExternalId} IdempotencyKey={IdempotencyKey} CredentialReference={CredentialReference} AccessTokenLength={AccessTokenLength} AccessTokenSha256Prefix={AccessTokenSha256Prefix}",
                url,
                (int)exception.StatusCode,
                exception.Content,
                companyId,
                companyChannelId,
                credential.PhoneNumberId,
                credential.BusinessAccountId,
                recipientExternalId ?? request.To,
                idempotencyKey,
                credential.CredentialReference,
                credential.AccessToken.Length,
                HashPrefix(credential.AccessToken));

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

    private static string ExtensionFromContentType(string contentType)
    {
        return contentType.ToLowerInvariant() switch
        {
            "audio/mpeg" => ".mp3",
            "audio/ogg" => ".ogg",
            "audio/aac" => ".aac",
            "audio/amr" => ".amr",
            "audio/mp4" => ".m4a",
            _ => ".bin",
        };
    }

    private string BuildMessagesUrl(string phoneNumberId)
    {
        var baseUrl = configuration["WhatsApp:GraphApiBaseUrl"] ?? DefaultGraphApiBaseUrl;
        var normalizedBaseUrl = baseUrl.EndsWith('/')
            ? baseUrl
            : $"{baseUrl}/";
        return new Uri(new Uri(normalizedBaseUrl), $"{Uri.EscapeDataString(phoneNumberId)}/messages").ToString();
    }

    private static string HashPrefix(string value)
    {
        var hash = SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(hash)[..12];
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
