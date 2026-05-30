namespace CeoAgent.Integrations.Messaging;

public sealed record ChannelTextMessage(
    Guid CompanyId,
    Guid CompanyChannelId,
    Guid ConversationId,
    Guid MessageId,
    string RecipientExternalId,
    string Text,
    string IdempotencyKey);
