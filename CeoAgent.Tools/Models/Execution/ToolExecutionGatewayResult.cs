namespace CeoAgent.Tools.Models.Execution;

public sealed record ToolExecutionGatewayResult(
    string ToolCallId,
    string ToolName,
    string Content);
