using System.Text.Json.Serialization;

namespace CeoAgent.Infrastructure.ApiClient.WhatsApp;

public sealed record WhatsAppSendMessageRequest(
    [property: JsonPropertyName("messaging_product")] string MessagingProduct,
    [property: JsonPropertyName("recipient_type")] string? RecipientType,
    [property: JsonPropertyName("to")] string? To,
    [property: JsonPropertyName("type")] string? Type,
    [property: JsonPropertyName("text")] WhatsAppTextBody? Text,
    [property: JsonPropertyName("status")] string? Status,
    [property: JsonPropertyName("message_id")] string? MessageId,
    [property: JsonPropertyName("biz_opaque_callback_data")] string? BizOpaqueCallbackData = null);

public sealed record WhatsAppTextBody(
    [property: JsonPropertyName("preview_url")] bool PreviewUrl,
    [property: JsonPropertyName("body")] string Body);

public sealed record WhatsAppSendMessageResponse(
    [property: JsonPropertyName("messages")] IReadOnlyList<WhatsAppSentMessage>? Messages);

public sealed record WhatsAppSentMessage(
    [property: JsonPropertyName("id")] string Id);
