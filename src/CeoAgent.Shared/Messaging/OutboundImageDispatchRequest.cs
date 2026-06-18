namespace CeoAgent.Shared.Messaging;

public sealed record OutboundImageDispatchRequest(
    Guid OrganizationId,
    Guid CompanyChannelId,
    Guid ConversationId,
    Guid MessageId,
    string RecipientExternalId,
    byte[] Content,
    string ContentType,
    string FileName,
    string Caption,
    string IdempotencyKey,
    string? CorrelationId = null);
