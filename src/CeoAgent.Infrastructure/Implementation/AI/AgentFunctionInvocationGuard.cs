using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CeoAgent.Application;
using CeoAgent.Shared.Enums;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace CeoAgent.Infrastructure.Implementation.AI;

public sealed partial class AgentFunctionInvocationGuard(
    AgentToolDispatcher dispatcher,
    AgentTurnContextAccessor turnContextAccessor,
    ILogger<AgentFunctionInvocationGuard> logger)
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public async ValueTask<object?> InvokeAsync(
        FunctionInvocationContext invocationContext,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(invocationContext);

        var turnContext = turnContextAccessor.Current
            ?? throw new InvalidOperationException("Agent turn context is not available.");

        turnContext.RecordToolInvocation();
        var functionName = invocationContext.Function.Name;
        var arguments = ToJsonElement(invocationContext.Arguments);
        var idempotencyKey = CreateIdempotencyKey(
            turnContext.ConversationId,
            turnContext.InboundMessageId,
            functionName,
            arguments);

        using var activity = StartToolActivity(turnContext, functionName);
        var stopwatch = Stopwatch.StartNew();
        try
        {
            var result = await dispatcher.DispatchAsync(
                turnContext,
                functionName,
                arguments,
                idempotencyKey,
                cancellationToken);

            stopwatch.Stop();
            if (result.WasDeniedBeforeExecution)
            {
                RecordToolDenied(activity, stopwatch, result.FailureReason ?? "tool_denied");
            }
            else if (result.Status is { } status)
            {
                RecordToolCompletion(activity, stopwatch, status, result.FailureReason);
            }

            return result.Content;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            stopwatch.Stop();
            CeoAgentTelemetry.ToolExecutionDuration.Record(stopwatch.ElapsedMilliseconds);
            CeoAgentTelemetry.ToolExecutionFailures.Add(1);
            activity?.SetTag("tool.status", "failed");
            activity?.SetTag(CeoAgentTelemetry.Langfuse.MetadataToolStatus, "failed");
            activity?.SetTag(CeoAgentTelemetry.Langfuse.MetadataToolFailureReason, exception.GetType().Name);
            activity?.SetTag(CeoAgentTelemetry.LangSmith.MetadataToolStatus, "failed");
            activity?.SetTag(CeoAgentTelemetry.LangSmith.MetadataToolFailureReason, exception.GetType().Name);
            activity?.SetStatus(ActivityStatusCode.Error, exception.GetType().Name);
            AgentFunctionInvocationFailed(logger, exception, functionName);
            throw;
        }
    }

    private static Activity? StartToolActivity(AgentTurnContext turnContext, string functionName)
    {
        var activity = CeoAgentTelemetry.ActivitySource.StartActivity("tool.execution");
        activity?.SetTag("organization.id", turnContext.OrganizationId);
        activity?.SetTag("conversation.id", turnContext.ConversationId);
        activity?.SetTag("tool.key", functionName);
        activity?.SetTag(CeoAgentTelemetry.Langfuse.ObservationType, "tool");
        activity?.SetTag(CeoAgentTelemetry.Langfuse.MetadataOrganizationId, turnContext.OrganizationId.ToString("D"));
        activity?.SetTag(CeoAgentTelemetry.Langfuse.MetadataConversationId, turnContext.ConversationId.ToString("D"));
        activity?.SetTag(CeoAgentTelemetry.Langfuse.MetadataToolKey, functionName);
        activity?.SetTag(CeoAgentTelemetry.LangSmith.SpanKind, "tool");
        activity?.SetTag(CeoAgentTelemetry.LangSmith.GenAiToolName, functionName);
        activity?.SetTag(CeoAgentTelemetry.LangSmith.MetadataOrganizationId, turnContext.OrganizationId.ToString("D"));
        activity?.SetTag(CeoAgentTelemetry.LangSmith.MetadataConversationId, turnContext.ConversationId.ToString("D"));
        return activity;
    }

    private static JsonElement ToJsonElement(AIFunctionArguments arguments)
    {
        var sorted = new SortedDictionary<string, object?>(StringComparer.Ordinal);
        foreach (var argument in arguments)
        {
            sorted[argument.Key] = argument.Value;
        }

        return JsonSerializer.SerializeToElement(sorted, SerializerOptions);
    }

    private static string CreateIdempotencyKey(
        Guid conversationId,
        Guid inboundMessageId,
        string functionName,
        JsonElement arguments)
    {
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(arguments.GetRawText()));
        var hash = Convert.ToHexString(hashBytes);
        return $"{conversationId:N}:{inboundMessageId:N}:{functionName}:{hash[..16]}";
    }

    private static void RecordToolCompletion(
        Activity? activity,
        Stopwatch stopwatch,
        ToolExecutionStatus status,
        string? failureReason)
    {
        CeoAgentTelemetry.ToolExecutionDuration.Record(stopwatch.ElapsedMilliseconds);
        var wireStatus = AgentToolResultContent.ToWireStatus(status);
        activity?.SetTag("tool.status", wireStatus);
        activity?.SetTag(CeoAgentTelemetry.Langfuse.MetadataToolStatus, wireStatus);
        activity?.SetTag(CeoAgentTelemetry.LangSmith.MetadataToolStatus, wireStatus);
        if (failureReason is not null)
        {
            CeoAgentTelemetry.ToolExecutionFailures.Add(1);
            activity?.SetTag("tool.failure_reason", failureReason);
            activity?.SetTag(CeoAgentTelemetry.Langfuse.MetadataToolFailureReason, failureReason);
            activity?.SetTag(CeoAgentTelemetry.LangSmith.MetadataToolFailureReason, failureReason);
        }

        activity?.SetStatus(ActivityStatusCode.Ok);
    }

    private static void RecordToolDenied(
        Activity? activity,
        Stopwatch stopwatch,
        string failureReason)
    {
        CeoAgentTelemetry.ToolExecutionDuration.Record(stopwatch.ElapsedMilliseconds);
        CeoAgentTelemetry.ToolExecutionFailures.Add(1);
        activity?.SetTag("tool.status", "denied");
        activity?.SetTag("tool.failure_reason", failureReason);
        activity?.SetTag(CeoAgentTelemetry.Langfuse.MetadataToolStatus, "denied");
        activity?.SetTag(CeoAgentTelemetry.Langfuse.MetadataToolFailureReason, failureReason);
        activity?.SetTag(CeoAgentTelemetry.LangSmith.MetadataToolStatus, "denied");
        activity?.SetTag(CeoAgentTelemetry.LangSmith.MetadataToolFailureReason, failureReason);
        activity?.SetStatus(ActivityStatusCode.Ok);
    }

    [LoggerMessage(
        EventId = 6101,
        Level = LogLevel.Error,
        Message = "AgentFunctionInvocationFailed ToolKey={ToolKey}")]
    private static partial void AgentFunctionInvocationFailed(
        ILogger logger,
        Exception exception,
        string toolKey);
}
