using System.ComponentModel;

namespace CeoAgent.Shared.Enums;

public enum ToolExecutionStatus
{
    [Description("The tool execution has been planned and stored but has not started yet.")]
    ToolExecutionWaitingToRun = 1,

    [Description("The tool execution is currently running.")]
    ToolExecutionInProgress = 2,

    [Description("The tool execution completed successfully.")]
    ToolExecutionSucceeded = 3,

    [Description("The tool execution failed due to a retryable or non-retryable error.")]
    ToolExecutionFailed = 4,

    [Description("The tool execution was denied because the tool is disabled, unauthorized or invalid for the organization.")]
    ToolExecutionDenied = 5,

    [Description("The tool execution failed temporarily and a retry has been scheduled.")]
    ToolExecutionRetryScheduled = 6,
}
