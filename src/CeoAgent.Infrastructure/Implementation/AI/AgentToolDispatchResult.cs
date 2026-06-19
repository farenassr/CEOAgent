using CeoAgent.Infrastructure.Entities;
using CeoAgent.Shared.Enums;

namespace CeoAgent.Infrastructure.Implementation.AI;

internal sealed record AgentToolDispatchResult(
    string Content,
    ToolExecutionStatus? Status,
    string? FailureReason,
    bool WasDeniedBeforeExecution)
{
    public static AgentToolDispatchResult FromExecution(string content, ToolExecution execution)
    {
        return new AgentToolDispatchResult(
            content,
            execution.Status,
            execution.FailureReason,
            WasDeniedBeforeExecution: false);
    }

    public static AgentToolDispatchResult DeniedWithoutExecution(
        string content,
        string failureReason)
    {
        return new AgentToolDispatchResult(
            content,
            ToolExecutionStatus.ToolExecutionDenied,
            failureReason,
            WasDeniedBeforeExecution: true);
    }

    public static AgentToolDispatchResult DeniedWithExecution(
        string content,
        ToolExecution execution)
    {
        return new AgentToolDispatchResult(
            content,
            execution.Status,
            execution.FailureReason,
            WasDeniedBeforeExecution: true);
    }
}
