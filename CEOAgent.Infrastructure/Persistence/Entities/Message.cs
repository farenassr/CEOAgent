using CEOAgent.Shared.Enums;

namespace CEOAgent.Infrastructure.Persistence.Entities;

public sealed class Message : AuditableCompanyOwnedEntity
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
    /// Channel type where this message belongs. Example: whatsapp_cloud.
    /// </summary>
    public required string ChannelType { get; set; }

    /// <summary>
    /// Text content or transcribed audio content. Example: I need a table for four tonight.
    /// </summary>
    public string? Text { get; set; }

    /// <summary>
    /// Provider-side message identifier used for idempotency. Example: wamid.HBgMNTczMDAxMTEyMjMz.
    /// </summary>
    public string? ProviderMessageId { get; set; }

    /// <summary>
    /// Raw or normalized provider payload stored as JSON. Example: {"type":"text","id":"wamid..."}.
    /// </summary>
    public string? PayloadJson { get; set; }

    /// <summary>
    /// UTC timestamp when the message occurred. Example: 2026-05-22T10:15:30Z.
    /// </summary>
    public DateTime OccurredAt { get; set; }

    /// <summary>
    /// Conversation navigation for this message. Example: the active customer conversation.
    /// </summary>
    public Conversation Conversation { get; set; } = null!;
}
