using Microsoft.Extensions.Logging;

namespace CeoAgent.Infrastructure.Implementation.AITools.Handoff;

public sealed partial class HumanHandoffToolExecutor
{
    private static readonly Func<ILogger, Guid, Guid, IDisposable?> ConversationScope =
        LoggerMessage.DefineScope<Guid, Guid>(
            "OrganizationId={OrganizationId} ConversationId={ConversationId}");

    private static IDisposable? BeginConversationScope(
        ILogger logger,
        Guid organizationId,
        Guid conversationId)
    {
        return ConversationScope(logger, organizationId, conversationId);
    }

    [LoggerMessage(
        EventId = 6006,
        Level = LogLevel.Warning,
        Message = "HumanHandoffToolNotConfigured Reason={Reason}")]
    private static partial void HumanHandoffToolNotConfigured(
        ILogger logger,
        string reason);

    [LoggerMessage(
        EventId = 6007,
        Level = LogLevel.Information,
        Message = "HumanHandoffEscalated CompanyChannelId={CompanyChannelId} IntegrationProvider={IntegrationProvider} HandoffTicketId={HandoffTicketId} Reason={Reason} EstimatedPickupAt={EstimatedPickupAt} EscalationChannel={EscalationChannel} NotifyUserCount={NotifyUserCount}")]
    private static partial void HumanHandoffEscalated(
        ILogger logger,
        Guid companyChannelId,
        string integrationProvider,
        string handoffTicketId,
        string reason,
        string estimatedPickupAt,
        string? escalationChannel,
        int notifyUserCount);

    [LoggerMessage(
        EventId = 6008,
        Level = LogLevel.Warning,
        Message = "HumanHandoffNotificationUnavailable HandoffTicketId={HandoffTicketId}")]
    private static partial void HumanHandoffNotificationUnavailable(
        ILogger logger,
        Exception exception,
        string handoffTicketId);
}
