using CeoAgent.Shared.Enums;
using CeoAgent.Infrastructure.Entities.JsonDocuments;

namespace CeoAgent.Infrastructure.Entities;

public sealed class Message : AuditableOrganizationOwnedEntity
{
    /// <summary>
    /// Unique message identifier. Example: 018f4f70-8b5f-7b4c-9d1a-0f6c1d7a2b36.
    /// </summary>
    public Guid Id { get; set; } = Guid.CreateVersion7();

    /// <summary>
    /// Conversation that contains this message. Example: 018f4f70-8b5f-7b4c-9d1a-0f6c1d7a2b34.
    /// </summary>
    public Guid ConversationId { get; set; }

    /// <summary>
    /// Role represented by this message. Example: User.
    /// </summary>
    public MessageRole Role { get; set; }

    /// <summary>
    /// Message content type. Example: Text.
    /// </summary>
    public MessageType Type { get; set; }

    /// <summary>
    /// Canonical textual message content, including normal text, STT transcript, or TTS source text. Example: I need a table for four tonight.
    /// </summary>
    public string? MessageText { get; set; }

    /// <summary>
    /// Provider-side message identifier used for idempotency. Example: wamid.HBgMNTczMDAxMTEyMjMz.
    /// </summary>
    public string? ProviderMessageId { get; set; }

    /// <summary>
    /// Raw or normalized provider payload stored as JSON. Example: {"providerType":"text","providerMessageId":"wamid..."}.
    /// </summary>
    public MessagePayload? Payload { get; set; }

    /// <summary>
    /// UTC timestamp when the message occurred. Example: 2026-05-22T10:15:30Z.
    /// </summary>
    public DateTime OccurredAt { get; set; }

    /// <summary>
    /// Conversation navigation for this message. Example: the active customer conversation.
    /// </summary>
    public Conversation Conversation { get; set; } = null!;

    /// <summary>
    /// Tool executions triggered by this assistant message. Example: check_availability call.
    /// </summary>
    public ICollection<ToolExecution> TriggeredToolExecutions { get; } = [];

    /// <summary>
    /// Tool executions whose result was recorded by this message. Example: tool result message.
    /// </summary>
    public ICollection<ToolExecution> ResultToolExecutions { get; } = [];
}
