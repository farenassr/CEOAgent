using System.Text.Json;
using CeoAgent.Infrastructure.Entities;
using CeoAgent.Shared.Enums;

namespace CeoAgent.Infrastructure.Implementation.AI;

internal static class AgentToolResultContent
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public static string Serialize(ToolExecution execution)
    {
        return JsonSerializer.Serialize(new
        {
            toolKey = execution.ToolKey,
            status = ToWireStatus(execution.Status),
            failureReason = execution.FailureReason,
            result = execution.Result,
        }, SerializerOptions);
    }

    public static string SerializeDenied(string functionName, string failureReason)
    {
        return JsonSerializer.Serialize(new
        {
            toolKey = functionName,
            status = "denied",
            failureReason,
        }, SerializerOptions);
    }

    public static string ToWireStatus(ToolExecutionStatus status)
    {
        return status switch
        {
            ToolExecutionStatus.ToolExecutionSucceeded => "succeeded",
            ToolExecutionStatus.ToolExecutionDenied => "denied",
            ToolExecutionStatus.ToolExecutionFailed => "failed",
            ToolExecutionStatus.ToolExecutionInProgress => "in_progress",
            ToolExecutionStatus.ToolExecutionRetryScheduled => "retry_scheduled",
            _ => "waiting_to_run",
        };
    }
}
