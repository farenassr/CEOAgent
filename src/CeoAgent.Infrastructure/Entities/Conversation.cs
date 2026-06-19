using CeoAgent.Shared.Enums;

namespace CeoAgent.Infrastructure.Entities;

public sealed class Conversation : AuditableOrganizationOwnedEntity
{
    /// <summary>
    /// Unique conversation identifier. Example: 018f4f70-8b5f-7b4c-9d1a-0f6c1d7a2b34.
    /// </summary>
    public Guid Id { get; set; } = Guid.CreateVersion7();

    /// <summary>
    /// Customer participating in the conversation. Example: 018f4f70-8b5f-7b4c-9d1a-0f6c1d7a2b33.
    /// </summary>
    public Guid CustomerId { get; set; }

    /// <summary>
    /// Company channel for the conversation. Example: 018f4f70-8b5f-7b4c-9d1a-0f6c1d7a2b31.
    /// </summary>
    public Guid CompanyChannelId { get; set; }

    /// <summary>
    /// Agent profile snapshot used by this conversation. Example: 018f4f70-8b5f-7b4c-9d1a-0f6c1d7a2b32.
    /// </summary>
    public Guid AgentProfileId { get; set; }

    /// <summary>
    /// LLM provider captured for this conversation. Example: OpenAI.
    /// </summary>
    public LlmProvider? LlmProvider { get; set; }

    /// <summary>
    /// Model captured for this conversation. Example: gpt-4.1-mini.
    /// </summary>
    public string? ModelName { get; set; }

    /// <summary>
    /// Provider-managed conversation or latest response identifier. Example: resp_123.
    /// </summary>
    public string? ProviderConversationId { get; set; }

    /// <summary>
    /// Latest provider response identifier. Example: resp_456.
    /// </summary>
    public string? ProviderLastResponseId { get; set; }

    /// <summary>
    /// Serialized Microsoft Agent Framework session state.
    /// </summary>
    public string? AgentSessionJson { get; set; }

    /// <summary>
    /// UTC timestamp when the active agent session started.
    /// </summary>
    public DateTime? AgentSessionStartedAt { get; set; }

    /// <summary>
    /// UTC timestamp when the active agent session was last used.
    /// </summary>
    public DateTime? AgentSessionLastUsedAt { get; set; }

    /// <summary>
    /// UTC timestamp when the active agent session should be discarded.
    /// </summary>
    public DateTime? AgentSessionExpiresAt { get; set; }

    /// <summary>
    /// Number of user turns processed through the active agent session.
    /// </summary>
    public int AgentSessionTurnCount { get; set; }

    /// <summary>
    /// Reason the provider session was last reset. Example: idle_expired.
    /// </summary>
    public string? AgentSessionResetReason { get; set; }

    /// <summary>
    /// Current conversation status. Example: Open.
    /// </summary>
    public ConversationStatus Status { get; set; } = ConversationStatus.Open;

    /// <summary>
    /// UTC timestamp for the most recent inbound or outbound message. Example: 2026-05-22T10:15:30Z.
    /// </summary>
    public DateTime LastMessageAt { get; set; }

    /// <summary>
    /// Customer navigation for this conversation. Example: Karina Perez.
    /// </summary>
    public Customer Customer { get; set; } = null!;

    /// <summary>
    /// Channel navigation for this conversation. Example: WhatsApp Cloud phone number.
    /// </summary>
    public CompanyChannel CompanyChannel { get; set; } = null!;

    /// <summary>
    /// Agent profile used when this conversation was created. Example: Spanish support assistant.
    /// </summary>
    public AgentProfile AgentProfile { get; set; } = null!;

    /// <summary>
    /// Short-lived state for the active interaction. Example: waiting for human handoff confirmation.
    /// </summary>
    public ConversationState? State { get; set; }

    /// <summary>
    /// Messages recorded for this conversation. Example: the last inbound user message.
    /// </summary>
    public ICollection<Message> Messages { get; } = [];
}
