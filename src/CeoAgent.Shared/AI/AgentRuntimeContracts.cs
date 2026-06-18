using System.Text.Json;
using CeoAgent.Shared.Enums;

namespace CeoAgent.Shared.AI;

public sealed record AgentRunRequest(
    LlmProvider Provider,
    string ModelName,
    string SystemPrompt,
    IReadOnlyList<AgentConversationMessage> Messages,
    IReadOnlyList<AgentToolDescriptor> Tools,
    int? MaxOutputTokenCount = null);

public sealed record AgentConversationMessage(
    string Role,
    string? Text,
    string? ToolCallId = null,
    string? ToolName = null,
    JsonElement? ToolArguments = null);

public sealed record AgentRunResult(
    string? AssistantText,
    IReadOnlyList<AgentToolCall> ToolCalls,
    string? ResponseId = null,
    string? FinishReason = null,
    int? InputTokenCount = null,
    int? OutputTokenCount = null,
    int? TotalTokenCount = null,
    double? EstimatedCostUsd = null);

public sealed record AgentToolCall(
    string Id,
    string Name,
    JsonElement Arguments);

public sealed record AgentToolDescriptor(
    Guid CompanyToolId,
    string Name,
    string Description,
    JsonElement ParametersSchema,
    bool IsMutating);
