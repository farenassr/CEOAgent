namespace CeoAgent.ApiService.Modules.WhatsApp;

public sealed record WhatsAppWebhookIngestionResult(
    bool Enqueued,
    Guid? CompanyId,
    Guid? ConversationId,
    Guid? MessageId);
