namespace CeoAgent.Shared.Messaging;

public sealed record OutboundTextDispatchRequest(
    Guid OrganizationId,
    Guid CompanyChannelId,
    Guid ConversationId,
    Guid MessageId,
    string RecipientExternalId,
    string Text,
    string IdempotencyKey,
    string? CorrelationId = null);
