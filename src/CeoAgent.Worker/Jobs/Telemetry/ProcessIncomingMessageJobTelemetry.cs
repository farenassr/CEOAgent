using System.Diagnostics;
using System.Text.Json;
using CeoAgent.Application;
using CeoAgent.Shared.AI;
using CeoAgent.Shared.Jobs;

namespace CeoAgent.Worker.Jobs.Telemetry;

internal sealed record ProcessIncomingMessageTelemetryContext(
    Guid OrganizationId,
    Guid ConversationId,
    Guid AgentProfileId,
    string Channel,
    string LlmProvider,
    string ModelName,
    int ToolCount);

internal static class ProcessIncomingMessageJobTelemetry
{
    public static Activity? StartMessageProcessing(ProcessIncomingMessageJob job, string messagingSystem)
    {
        var activity = CeoAgentTelemetry.ActivitySource.StartActivity("whatsapp.message.process", ActivityKind.Internal);
        activity?.SetTag("organization.id", job.OrganizationId);
        activity?.SetTag("conversation.id", job.ConversationId);
        activity?.SetTag("message.id", job.MessageId);
        activity?.SetTag("messaging.system", messagingSystem);
        activity?.SetTag(CeoAgentTelemetry.LangSmith.SpanKind, "chain");
        activity?.SetTag(CeoAgentTelemetry.LangSmith.MetadataOrganizationId, job.OrganizationId.ToString("D"));
        activity?.SetTag(CeoAgentTelemetry.LangSmith.MetadataConversationId, job.ConversationId.ToString("D"));
        activity?.SetTag(CeoAgentTelemetry.LangSmith.MetadataInboundMessageId, job.MessageId.ToString("D"));

        if (!string.IsNullOrWhiteSpace(job.CorrelationId))
        {
            activity?.SetTag("correlation.id", job.CorrelationId);
            activity?.SetTag(CeoAgentTelemetry.LangSmith.MetadataCorrelationId, job.CorrelationId);
        }

        return activity;
    }

    public static void EnrichMessageProcessing(
        Activity? activity,
        ProcessIncomingMessageTelemetryContext context)
    {
        if (activity is null)
        {
            return;
        }

        activity.SetTag("channel", context.Channel);
        activity.SetTag("agent.profile.id", context.AgentProfileId);
        activity.SetTag("llm.provider", context.LlmProvider);
        activity.SetTag("llm.model", context.ModelName);
        activity.SetTag(CeoAgentTelemetry.LangSmith.MetadataChannel, context.Channel);
        activity.SetTag(CeoAgentTelemetry.LangSmith.MetadataProvider, context.LlmProvider);
    }

    public static Activity? StartAgentIteration(
        ProcessIncomingMessageTelemetryContext context,
        int iteration)
    {
        var activity = CeoAgentTelemetry.ActivitySource.StartActivity("agent.iteration", ActivityKind.Internal);
        SetCommonAgentTags(activity, context, iteration);
        activity?.SetTag(CeoAgentTelemetry.LangSmith.SpanKind, "chain");
        return activity;
    }

    public static Activity? StartLlmGeneration(
        ProcessIncomingMessageTelemetryContext context,
        int iteration)
    {
        var activity = CeoAgentTelemetry.ActivitySource.StartActivity("llm.generation", ActivityKind.Internal);
        SetCommonAgentTags(activity, context, iteration);
        activity?.SetTag(CeoAgentTelemetry.Langfuse.ObservationType, "generation");
        activity?.SetTag(CeoAgentTelemetry.Langfuse.ObservationModelName, context.ModelName);
        activity?.SetTag(CeoAgentTelemetry.Langfuse.MetadataProvider, context.LlmProvider);
        activity?.SetTag(CeoAgentTelemetry.Langfuse.MetadataOrganizationId, context.OrganizationId.ToString("D"));
        activity?.SetTag(CeoAgentTelemetry.Langfuse.MetadataConversationId, context.ConversationId.ToString("D"));
        activity?.SetTag(CeoAgentTelemetry.Langfuse.MetadataChannel, context.Channel);
        activity?.SetTag(CeoAgentTelemetry.LangSmith.SpanKind, "llm");
        activity?.SetTag(CeoAgentTelemetry.LangSmith.GenAiSystem, "openai");
        activity?.SetTag(CeoAgentTelemetry.LangSmith.GenAiRequestModel, context.ModelName);
        return activity;
    }

    public static void RecordLlmDuration(TimeSpan elapsed)
    {
        CeoAgentTelemetry.LlmCallDuration.Record(elapsed.TotalMilliseconds);
    }

    public static void RecordTokenUsage(AgentRunResult agentResult)
    {
        if (agentResult.TotalTokenCount is { } totalTokens)
        {
            CeoAgentTelemetry.LlmTokensConsumed.Add(totalTokens);
        }

        if (agentResult.EstimatedCostUsd is { } estimatedCost)
        {
            CeoAgentTelemetry.LlmEstimatedCost.Add(estimatedCost);
        }
    }

    public static void EnrichLlmGenerationResult(
        Activity? activity,
        AgentRunResult agentResult,
        string modelName)
    {
        if (activity is null)
        {
            return;
        }

        activity.SetTag("llm.response.id", agentResult.ResponseId);
        activity.SetTag("llm.finish_reason", agentResult.FinishReason);
        activity.SetTag("llm.tool_call_count", agentResult.ToolCalls.Count);
        SetLangfuseUsageTags(activity, agentResult);
        SetLangSmithUsageTags(activity, agentResult, modelName);
    }

    public static void MarkOk(Activity? activity)
    {
        activity?.SetStatus(ActivityStatusCode.Ok);
    }

    public static void MarkError(Activity? activity, Exception exception)
    {
        activity?.SetStatus(ActivityStatusCode.Error, exception.GetType().Name);
    }

    private static void SetCommonAgentTags(
        Activity? activity,
        ProcessIncomingMessageTelemetryContext context,
        int iteration)
    {
        activity?.SetTag("organization.id", context.OrganizationId);
        activity?.SetTag("conversation.id", context.ConversationId);
        activity?.SetTag("channel", context.Channel);
        activity?.SetTag("llm.provider", context.LlmProvider);
        activity?.SetTag("llm.model", context.ModelName);
        activity?.SetTag("agent.iteration", iteration);
        activity?.SetTag("tool.count", context.ToolCount);
        activity?.SetTag(CeoAgentTelemetry.LangSmith.MetadataProvider, context.LlmProvider);
        activity?.SetTag(CeoAgentTelemetry.LangSmith.MetadataOrganizationId, context.OrganizationId.ToString("D"));
        activity?.SetTag(CeoAgentTelemetry.LangSmith.MetadataConversationId, context.ConversationId.ToString("D"));
        activity?.SetTag(CeoAgentTelemetry.LangSmith.MetadataChannel, context.Channel);
    }

    private static void SetLangfuseUsageTags(Activity activity, AgentRunResult agentResult)
    {
        var usage = new Dictionary<string, int>(capacity: 3);
        if (agentResult.InputTokenCount is { } inputTokens)
        {
            usage["input"] = inputTokens;
        }

        if (agentResult.OutputTokenCount is { } outputTokens)
        {
            usage["output"] = outputTokens;
        }

        if (agentResult.TotalTokenCount is { } totalTokens)
        {
            usage["total"] = totalTokens;
        }

        if (usage.Count > 0)
        {
            activity.SetTag(CeoAgentTelemetry.Langfuse.ObservationUsageDetails, JsonSerializer.Serialize(usage));
        }
    }

    private static void SetLangSmithUsageTags(Activity activity, AgentRunResult agentResult, string modelName)
    {
        activity.SetTag(CeoAgentTelemetry.LangSmith.GenAiResponseModel, modelName);

        if (agentResult.InputTokenCount is { } inputTokens)
        {
            activity.SetTag(CeoAgentTelemetry.LangSmith.GenAiUsageInputTokens, inputTokens);
        }

        if (agentResult.OutputTokenCount is { } outputTokens)
        {
            activity.SetTag(CeoAgentTelemetry.LangSmith.GenAiUsageOutputTokens, outputTokens);
        }

        if (agentResult.TotalTokenCount is { } totalTokens)
        {
            activity.SetTag(CeoAgentTelemetry.LangSmith.GenAiUsageTotalTokens, totalTokens);
        }
    }
}
