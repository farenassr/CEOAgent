namespace CeoAgent.Shared.Messaging;

public sealed record ChannelImageMessage(
    Guid OrganizationId,
    Guid CompanyChannelId,
    Guid ConversationId,
    Guid MessageId,
    string RecipientExternalId,
    byte[] Content,
    string ContentType,
    string FileName,
    string Caption,
    string IdempotencyKey);
