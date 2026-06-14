using System.Diagnostics;
using CeoAgent.Shared.Jobs;
using Microsoft.Extensions.Logging;

namespace CeoAgent.Worker.Jobs;

public sealed partial class IncomingMessageQueueWorker
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
        EventId = 2203,
        Level = LogLevel.Warning,
        Message = "IncomingQueueBacklogTelemetryFailed")]
    private static partial void IncomingQueueBacklogTelemetryFailed(
        ILogger logger,
        Exception exception);

    [LoggerMessage(
        EventId = 2204,
        Level = LogLevel.Warning,
        Message = "IncomingQueueReceiveFailed")]
    private static partial void IncomingQueueReceiveFailed(
        ILogger logger,
        Exception exception);

    [LoggerMessage(
        EventId = 2201,
        Level = LogLevel.Information,
        Message = "IncomingQueueMessageProcessed MessageId={MessageId} AttemptCount={AttemptCount} ElapsedMilliseconds={ElapsedMilliseconds} Status={Status}")]
    private static partial void IncomingQueueMessageProcessed(
        ILogger logger,
        string messageId,
        long attemptCount,
        long elapsedMilliseconds,
        string status);

    [LoggerMessage(
        EventId = 2202,
        Level = LogLevel.Error,
        Message = "IncomingQueueMessageFailed MessageId={MessageId} AttemptCount={AttemptCount} ElapsedMilliseconds={ElapsedMilliseconds} Status={Status}")]
    private static partial void IncomingQueueMessageFailed(
        ILogger logger,
        Exception exception,
        string messageId,
        long attemptCount,
        long elapsedMilliseconds,
        string status);

    [LoggerMessage(
        EventId = 2205,
        Level = LogLevel.Warning,
        Message = "IncomingQueueMessagePoisoned MessageId={MessageId} AttemptCount={AttemptCount} Status={Status}")]
    private static partial void IncomingQueueMessagePoisoned(
        ILogger logger,
        string messageId,
        long attemptCount,
        string status);

    [LoggerMessage(
        EventId = 2206,
        Level = LogLevel.Warning,
        Message = "IncomingMessageVisibilityRenewalFailed MessageId={MessageId}")]
    private static partial void IncomingMessageVisibilityRenewalFailed(
        ILogger logger,
        Exception exception,
        string messageId);
}
