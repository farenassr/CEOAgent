using CEOAgent.Shared.Enums;

namespace CEOAgent.Infrastructure.Persistence.Entities;

public sealed class Conversation : AuditableCompanyOwnedEntity
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
    /// Channel type for the conversation. Example: whatsapp_cloud.
    /// </summary>
    public required string ChannelType { get; set; }

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
    /// Short-lived state for the active interaction. Example: waiting for human handoff confirmation.
    /// </summary>
    public ConversationState? State { get; set; }

    /// <summary>
    /// Messages recorded for this conversation. Example: the last inbound user message.
    /// </summary>
    public ICollection<Message> Messages { get; } = [];
}
