namespace CeoAgent.ApiService.Modules.WhatsApp;

public sealed class InvalidWhatsAppWebhookPayloadException : Exception
{
    public InvalidWhatsAppWebhookPayloadException()
        : base("WhatsApp webhook payload is not valid JSON.")
    {
    }
}
