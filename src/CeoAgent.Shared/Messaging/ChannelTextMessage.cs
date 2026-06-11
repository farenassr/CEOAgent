namespace CeoAgent.Shared.Messaging;

public sealed record ChannelTextMessage(
    Guid OrganizationId,
    Guid CompanyChannelId,
    Guid ConversationId,
    Guid MessageId,
    string RecipientExternalId,
    string Text,
    string IdempotencyKey);
