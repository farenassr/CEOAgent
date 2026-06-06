using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace CeoAgent.Application;

public static class CeoAgentTelemetry
{
    public const string SourceName = "CeoAgent.Application";
    public static readonly ActivitySource ActivitySource = new(SourceName);

    public static readonly Meter Meter = new(SourceName);

    public static readonly Histogram<double> LlmCallDuration = Meter.CreateHistogram<double>("llm.calls.duration", "ms");
    public static readonly Counter<long> LlmTokensConsumed = Meter.CreateCounter<long>("llm.tokens.consumed");
    public static readonly Counter<double> LlmEstimatedCost = Meter.CreateCounter<double>("llm.estimated_cost", "USD");
    public static readonly Histogram<double> ToolExecutionDuration = Meter.CreateHistogram<double>("tools.execution.duration", "ms");
    public static readonly Counter<long> ToolExecutionFailures = Meter.CreateCounter<long>("tools.execution.failures");

    public static readonly Counter<long> QueueDequeueCount = Meter.CreateCounter<long>("queue.messages.dequeued");
    public static readonly Counter<long> QueuePoisonCount = Meter.CreateCounter<long>("queue.messages.poisoned");
    public static readonly Histogram<double> QueueProcessingDuration = Meter.CreateHistogram<double>("queue.processing.duration", "ms");

    private static long _queueBacklog;
    public static void SetQueueBacklog(long backlog) => _queueBacklog = backlog;

    static CeoAgentTelemetry()
    {
        Meter.CreateObservableGauge("queue.backlog", () => _queueBacklog);
    }
}
