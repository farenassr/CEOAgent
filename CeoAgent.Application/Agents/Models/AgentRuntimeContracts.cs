using System.Text.Json;

namespace CeoAgent.Application.Agents;

public sealed record AgentRunRequest(
    string ModelName,
    string SystemPrompt,
    IReadOnlyList<AgentConversationMessage> Messages,
    IReadOnlyList<EnabledToolDescriptor> Tools);

public sealed record AgentConversationMessage(
    string Role,
    string? Text);

public sealed record AgentRunResult(
    string? AssistantText,
    IReadOnlyList<AgentToolCall> ToolCalls);

public sealed record AgentToolCall(
    string Id,
    string Name,
    JsonElement Arguments);
