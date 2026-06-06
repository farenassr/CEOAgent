namespace CeoAgent.Shared.AITools;

public sealed record ToolExecutionGatewayResult(
    string ToolCallId,
    string ToolName,
    string Content);
