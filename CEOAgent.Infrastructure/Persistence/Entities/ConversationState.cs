namespace CEOAgent.Infrastructure.Persistence.Entities;

public sealed class ConversationState : AuditableCompanyOwnedEntity
{
    /// <summary>
    /// Unique conversation state identifier. Example: 018f4f70-8b5f-7b4c-9d1a-0f6c1d7a2b35.
    /// </summary>
    public Guid Id { get; set; } = Guid.CreateVersion7();

    /// <summary>
    /// Conversation that owns this state. Example: 018f4f70-8b5f-7b4c-9d1a-0f6c1d7a2b34.
    /// </summary>
    public Guid ConversationId { get; set; }

    /// <summary>
    /// Serialized short-lived state for the active interaction. Example: {"intent":"human_handoff_request"}.
    /// </summary>
    public required string StateJson { get; set; }

    /// <summary>
    /// Conversation navigation for this state. Example: the open WhatsApp conversation.
    /// </summary>
    public Conversation Conversation { get; set; } = null!;
}
