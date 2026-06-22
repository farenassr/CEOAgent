using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using CeoAgent.Shared.AI;
using Microsoft.Agents.AI;
using ConversationEntity = CeoAgent.Infrastructure.Entities.Conversation;

namespace CeoAgent.Infrastructure.Implementation.AI;

internal static class AgentRuntimeSessionState
{
    internal static readonly JsonSerializerOptions SessionJsonOptions = new(JsonSerializerDefaults.Web)
    {
        TypeInfoResolver = new DefaultJsonTypeInfoResolver(),
    };

    public static async Task<ChatClientAgentSession> CreateSessionAsync(
        ChatClientAgent agent,
        ConversationEntity conversation,
        string? resetReason,
        CancellationToken cancellationToken)
    {
        if (resetReason is not null)
        {
            return (ChatClientAgentSession)await agent.CreateSessionAsync(cancellationToken);
        }

        if (!string.IsNullOrWhiteSpace(conversation.AgentSessionJson))
        {
            using var document = JsonDocument.Parse(conversation.AgentSessionJson);
            return (ChatClientAgentSession)await agent.DeserializeSessionAsync(
                document.RootElement,
                SessionJsonOptions,
                cancellationToken);
        }

        if (!string.IsNullOrWhiteSpace(conversation.ProviderConversationId))
        {
            var session = await agent.CreateSessionAsync(
                conversation.ProviderConversationId,
                cancellationToken);
            return (ChatClientAgentSession)session;
        }

        return (ChatClientAgentSession)await agent.CreateSessionAsync(cancellationToken);
    }

    public static string? ResolveSessionResetReason(
        ConversationEntity conversation,
        AgentTurnRequest request,
        DateTime now,
        AgentRuntimeOptions runtimeOptions)
    {
        if (conversation.LlmProvider is not null && conversation.LlmProvider != request.Provider)
        {
            return "provider_changed";
        }

        if (!string.IsNullOrWhiteSpace(conversation.ModelName)
            && !string.Equals(conversation.ModelName, request.ModelName, StringComparison.Ordinal))
        {
            return "model_changed";
        }

        if (conversation.AgentSessionTurnCount <= 0
            || string.IsNullOrWhiteSpace(conversation.AgentSessionJson)
                && string.IsNullOrWhiteSpace(conversation.ProviderConversationId))
        {
            return "new_session";
        }

        if (conversation.AgentSessionExpiresAt is { } expiresAt && expiresAt <= now)
        {
            return "idle_expired";
        }

        if (conversation.AgentSessionLastUsedAt is { } lastUsed
            && lastUsed.AddHours(runtimeOptions.SessionIdleExpirationHours) <= now)
        {
            return "idle_expired";
        }

        if (runtimeOptions.MaxSessionTurns > 0
            && conversation.AgentSessionTurnCount >= runtimeOptions.MaxSessionTurns)
        {
            return "max_turns";
        }

        return null;
    }

    public static void ApplyConversationSnapshot(
        ConversationEntity conversation,
        AgentTurnRequest request)
    {
        conversation.LlmProvider ??= request.Provider;
        conversation.ModelName ??= request.ModelName;
    }

    public static async Task<string> SerializeSessionAsync(
        ChatClientAgent agent,
        ChatClientAgentSession session,
        CancellationToken cancellationToken)
    {
        return (await agent.SerializeSessionAsync(
            session,
            SessionJsonOptions,
            cancellationToken)).GetRawText();
    }

    public static void ApplyConversationSession(
        ConversationEntity conversation,
        string? providerConversationId,
        string? responseId,
        string sessionJson,
        string? resetReason,
        DateTime now,
        AgentRuntimeOptions runtimeOptions)
    {
        conversation.ProviderConversationId = providerConversationId;
        conversation.ProviderLastResponseId = responseId;
        conversation.AgentSessionJson = sessionJson;
        conversation.AgentSessionStartedAt = resetReason is null && conversation.AgentSessionStartedAt is not null
            ? conversation.AgentSessionStartedAt
            : now;
        conversation.AgentSessionLastUsedAt = now;
        conversation.AgentSessionExpiresAt = now.AddHours(runtimeOptions.SessionIdleExpirationHours);
        conversation.AgentSessionTurnCount = resetReason is null
            ? conversation.AgentSessionTurnCount + 1
            : 1;
        conversation.AgentSessionResetReason = resetReason;
    }
}
