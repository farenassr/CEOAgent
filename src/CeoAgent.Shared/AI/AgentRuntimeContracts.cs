using CeoAgent.Shared.Enums;

namespace CeoAgent.Shared.AI;

public sealed record AgentTurnRequest(
    Guid OrganizationId,
    Guid ConversationId,
    Guid InboundMessageId,
    LlmProvider Provider,
    string ModelName,
    string SystemPrompt,
    string UserMessage,
    int? MaxOutputTokenCount = null,
    string? CorrelationId = null,
    bool MutatingToolsEnabled = true,
    string? MutatingToolsDisabledReason = null);

public sealed record AgentTurnResult(
    string? AssistantText,
    string? ResponseId = null,
    string? ProviderConversationId = null,
    string? FinishReason = null,
    int? InputTokenCount = null,
    int? OutputTokenCount = null,
    int? TotalTokenCount = null,
    double? EstimatedCostUsd = null,
    int ToolInvocationCount = 0,
    bool SessionWasReset = false,
    string? SessionResetReason = null);
