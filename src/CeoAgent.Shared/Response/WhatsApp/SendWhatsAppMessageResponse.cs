namespace CeoAgent.Shared.Response.WhatsApp;

public sealed class SendWhatsAppMessageResponse
{
    /// <summary>
    /// Provider-side WhatsApp message identifier.
    /// </summary>
    public string ProviderMessageId { get; set; } = string.Empty;
}
