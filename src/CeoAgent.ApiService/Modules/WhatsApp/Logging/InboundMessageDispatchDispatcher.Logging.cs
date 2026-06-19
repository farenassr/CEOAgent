using Microsoft.Extensions.Logging;

namespace CeoAgent.ApiService.Modules.WhatsApp;

public sealed partial class InboundMessageDispatchDispatcher
{
    [LoggerMessage(
        EventId = 2101,
        Level = LogLevel.Information,
        Message = "InboundMessageDispatchSucceeded OrganizationId={OrganizationId} ConversationId={ConversationId} MessageId={MessageId} DispatchId={DispatchId} AttemptCount={AttemptCount}")]
    private static partial void InboundMessageDispatchSucceeded(
        ILogger logger,
        Guid organizationId,
        Guid conversationId,
        Guid messageId,
        Guid dispatchId,
        int attemptCount);

    [LoggerMessage(
        EventId = 2102,
        Level = LogLevel.Warning,
        Message = "InboundMessageDispatchFailed OrganizationId={OrganizationId} ConversationId={ConversationId} MessageId={MessageId} DispatchId={DispatchId} AttemptCount={AttemptCount}")]
    private static partial void InboundMessageDispatchFailed(
        ILogger logger,
        Exception exception,
        Guid organizationId,
        Guid conversationId,
        Guid messageId,
        Guid dispatchId,
        int attemptCount);
}
