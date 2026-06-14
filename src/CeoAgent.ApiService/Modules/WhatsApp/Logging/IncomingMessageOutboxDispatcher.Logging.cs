using Microsoft.Extensions.Logging;

namespace CeoAgent.ApiService.Modules.WhatsApp;

public sealed partial class IncomingMessageOutboxDispatcher
{
    [LoggerMessage(
        EventId = 2101,
        Level = LogLevel.Information,
        Message = "IncomingMessageOutboxDispatchSucceeded OrganizationId={OrganizationId} ConversationId={ConversationId} MessageId={MessageId} OutboxId={OutboxId} AttemptCount={AttemptCount}")]
    private static partial void IncomingMessageOutboxDispatchSucceeded(
        ILogger logger,
        Guid organizationId,
        Guid conversationId,
        Guid messageId,
        Guid outboxId,
        int attemptCount);

    [LoggerMessage(
        EventId = 2102,
        Level = LogLevel.Warning,
        Message = "IncomingMessageOutboxDispatchFailed OrganizationId={OrganizationId} ConversationId={ConversationId} MessageId={MessageId} OutboxId={OutboxId} AttemptCount={AttemptCount}")]
    private static partial void IncomingMessageOutboxDispatchFailed(
        ILogger logger,
        Exception exception,
        Guid organizationId,
        Guid conversationId,
        Guid messageId,
        Guid outboxId,
        int attemptCount);
}
