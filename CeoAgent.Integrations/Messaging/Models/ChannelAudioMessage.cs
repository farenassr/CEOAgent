namespace CeoAgent.Integrations.Messaging;

public sealed record ChannelAudioMessage(
    Guid CompanyId,
    Guid CompanyChannelId,
    Guid ConversationId,
    Guid MessageId,
    string RecipientExternalId,
    Uri AudioUri,
    string ContentType,
    string IdempotencyKey);
