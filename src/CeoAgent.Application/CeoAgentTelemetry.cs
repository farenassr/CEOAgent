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
    public static readonly Counter<long> LlmBudgetExceeded = Meter.CreateCounter<long>("llm.budget.exceeded");
    public static readonly Counter<long> LlmCostEstimationMissing = Meter.CreateCounter<long>("llm.cost_estimation.missing");
    public static readonly Histogram<double> ToolExecutionDuration = Meter.CreateHistogram<double>("tools.execution.duration", "ms");
    public static readonly Counter<long> ToolExecutionFailures = Meter.CreateCounter<long>("tools.execution.failures");
    public static readonly Counter<long> HumanHandoffEscalations = Meter.CreateCounter<long>("handoff.escalations");
    public static readonly Counter<long> HumanHandoffNotificationsUnavailable = Meter.CreateCounter<long>("handoff.notifications.unavailable");

    public static readonly Counter<long> QueueDequeueCount = Meter.CreateCounter<long>("queue.messages.dequeued");
    public static readonly Counter<long> QueuePoisonCount = Meter.CreateCounter<long>("queue.messages.poisoned");
    public static readonly Histogram<double> QueueProcessingDuration = Meter.CreateHistogram<double>("queue.processing.duration", "ms");

    private static long _queueBacklog;
    public static void SetQueueBacklog(long backlog) => _queueBacklog = backlog;

    static CeoAgentTelemetry()
    {
        Meter.CreateObservableGauge("queue.backlog", () => _queueBacklog);
    }

    public static class Langfuse
    {
        public const string ObservationType = "langfuse.observation.type";
        public const string ObservationModelName = "langfuse.observation.model.name";
        public const string ObservationUsageDetails = "langfuse.observation.usage_details";
        public const string MetadataProvider = "langfuse.observation.metadata.provider";
        public const string MetadataOrganizationId = "langfuse.observation.metadata.organization_id";
        public const string MetadataConversationId = "langfuse.observation.metadata.conversation_id";
        public const string MetadataChannel = "langfuse.observation.metadata.channel";
        public const string MetadataToolKey = "langfuse.observation.metadata.tool_key";
        public const string MetadataToolStatus = "langfuse.observation.metadata.tool_status";
        public const string MetadataToolFailureReason = "langfuse.observation.metadata.tool_failure_reason";
        public const string MetadataToolMutating = "langfuse.observation.metadata.tool_mutating";
    }

    public static class LangSmith
    {
        public const string SpanKind = "langsmith.span.kind";
        public const string MetadataOrganizationId = "langsmith.metadata.organization_id";
        public const string MetadataConversationId = "langsmith.metadata.conversation_id";
        public const string MetadataInboundMessageId = "langsmith.metadata.inbound_message_id";
        public const string MetadataCorrelationId = "langsmith.metadata.correlation_id";
        public const string MetadataChannel = "langsmith.metadata.channel";
        public const string MetadataProvider = "langsmith.metadata.provider";
        public const string MetadataToolStatus = "langsmith.metadata.tool_status";
        public const string MetadataToolFailureReason = "langsmith.metadata.tool_failure_reason";
        public const string GenAiSystem = "gen_ai.system";
        public const string GenAiRequestModel = "gen_ai.request.model";
        public const string GenAiResponseModel = "gen_ai.response.model";
        public const string GenAiUsageInputTokens = "gen_ai.usage.input_tokens";
        public const string GenAiUsageOutputTokens = "gen_ai.usage.output_tokens";
        public const string GenAiUsageTotalTokens = "gen_ai.usage.total_tokens";
        public const string GenAiToolName = "gen_ai.tool.name";
    }
}
