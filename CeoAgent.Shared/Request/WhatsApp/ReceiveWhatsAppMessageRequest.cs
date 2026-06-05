using System.ComponentModel;

namespace CeoAgent.Shared.Request.WhatsApp;

public sealed class ReceiveWhatsAppMessageRequest
{
    /// <summary>
    /// Text to persist as an inbound WhatsApp customer message.
    /// </summary>
    [Description("Text to persist as an inbound WhatsApp customer message.")]
    public string MessageText { get; set; } = string.Empty;

    /// <summary>
    /// WhatsApp customer phone number or stable external identifier.
    /// </summary>
    [Description("WhatsApp customer phone number or stable external identifier.")]
    public string ExternalCustomerId { get; set; } = string.Empty;
}
