namespace CeoAgent.ApiService.Modules.WhatsApp;

public sealed record WhatsAppWebhookIngestionResult(
    bool Enqueued,
    Guid? OrganizationId,
    Guid? ConversationId,
    Guid? MessageId);
