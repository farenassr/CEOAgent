using System.ComponentModel;

namespace CeoAgent.Shared.Request.WhatsApp;

public sealed class SendWhatsAppMessageRequest
{
    /// <summary>
    /// Conversation that owns the outbound manual WhatsApp message.
    /// </summary>
    [Description("Conversation that owns the outbound manual WhatsApp message.")]
    public Guid ConversationId { get; set; }

    /// <summary>
    /// Provider-side WhatsApp customer identifier. Example: 573001112233.
    /// </summary>
    [Description("Provider-side WhatsApp customer identifier without a leading plus sign.")]
    public string RecipientExternalId { get; set; } = string.Empty;

    /// <summary>
    /// Text message to send through WhatsApp Cloud.
    /// </summary>
    [Description("Text message to send through WhatsApp Cloud.")]
    public string Text { get; set; } = string.Empty;

    /// <summary>
    /// Optional caller-provided idempotency key for manual sends.
    /// </summary>
    [Description("Optional caller-provided idempotency key for manual sends.")]
    public string? IdempotencyKey { get; set; }
}
