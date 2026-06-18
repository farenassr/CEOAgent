using System.Diagnostics;
using CeoAgent.Shared.Jobs;
using Microsoft.Extensions.Logging;

namespace CeoAgent.Worker.Jobs;

public sealed partial class ProcessIncomingMessageJobProcessor
{
    private static readonly Func<ILogger, string?, Guid, Guid, Guid?, string?, IDisposable?> JobScope =
        LoggerMessage.DefineScope<string?, Guid, Guid, Guid?, string?>(
            "CorrelationId={CorrelationId} OrganizationId={OrganizationId} ConversationId={ConversationId} JobId={JobId} TraceId={TraceId}");

    private IDisposable? BeginJobScope(ProcessIncomingMessageJob job)
    {
        return JobScope(
            logger,
            job.CorrelationId,
            job.OrganizationId,
            job.ConversationId,
            job.JobId,
            Activity.Current?.TraceId.ToString());
    }

    [LoggerMessage(
        EventId = 6005,
        Level = LogLevel.Information,
        Message = "InboundSuppressedDuringHandoff MessageId={MessageId}")]
    private static partial void InboundSuppressedDuringHandoff(
        ILogger logger,
        Guid messageId);

    [LoggerMessage(
        EventId = 2207,
        Level = LogLevel.Warning,
        Message = "ConversationConcurrencyConflict OrganizationId={OrganizationId} ConversationId={ConversationId} JobId={JobId}")]
    private static partial void ConversationConcurrencyConflict(
        ILogger logger,
        Guid organizationId,
        Guid conversationId,
        Guid? jobId);

    [LoggerMessage(
        EventId = 6001,
        Level = LogLevel.Error,
        Message = "AgentRuntimeFailed Iteration={Iteration}")]
    private static partial void AgentRuntimeFailed(
        ILogger logger,
        Exception exception,
        int iteration);

    [LoggerMessage(
        EventId = 6002,
        Level = LogLevel.Information,
        Message = "ToolCallRequested ToolName={ToolName} Iteration={Iteration} SideEffectsEnabled={SideEffectsEnabled}")]
    private static partial void ToolCallRequested(
        ILogger logger,
        string toolName,
        int iteration,
        bool sideEffectsEnabled);

    [LoggerMessage(
        EventId = 6003,
        Level = LogLevel.Warning,
        Message = "AgentLoopCapReached MaxIterations={MaxIterations}")]
    private static partial void AgentLoopCapReached(
        ILogger logger,
        int maxIterations);

    [LoggerMessage(
        EventId = 6006,
        Level = LogLevel.Warning,
        Message = "LlmCostPricingMissing OrganizationId={OrganizationId} AgentProfileId={AgentProfileId} ModelName={ModelName} EnvironmentName={EnvironmentName}")]
    private static partial void LlmCostPricingMissing(
        ILogger logger,
        Guid organizationId,
        Guid agentProfileId,
        string modelName,
        string environmentName);

    [LoggerMessage(
        EventId = 6007,
        Level = LogLevel.Warning,
        Message = "LlmBudgetExceeded OrganizationId={OrganizationId} AgentProfileId={AgentProfileId} ModelName={ModelName} EstimatedCostUsd={EstimatedCostUsd} MaxEstimatedCostUsd={MaxEstimatedCostUsd}")]
    private static partial void LlmBudgetExceeded(
        ILogger logger,
        Guid organizationId,
        Guid agentProfileId,
        string modelName,
        double estimatedCostUsd,
        double maxEstimatedCostUsd);

    [LoggerMessage(
        EventId = 6004,
        Level = LogLevel.Error,
        Message = "AutoHandoffEscalationFailed")]
    private static partial void AutoHandoffEscalationFailed(
        ILogger logger,
        Exception exception);
}
