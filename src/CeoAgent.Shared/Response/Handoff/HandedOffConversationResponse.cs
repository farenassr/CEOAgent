namespace CeoAgent.Shared.Response.Handoff;

/// <summary>
/// Sanitized admin view of a conversation currently handed off to a human operator.
/// Contains identifiers and categorical metadata only; never raw customer text or phone numbers.
/// </summary>
public sealed class HandedOffConversationResponse
{
    /// <summary>
    /// Conversation handed off to a human. Example: 018f4f70-8b5f-7b4c-9d1a-0f6c1d7a2b34.
    /// </summary>
    public Guid ConversationId { get; set; }

    /// <summary>
    /// Customer participating in the conversation. Example: 018f4f70-8b5f-7b4c-9d1a-0f6c1d7a2b33.
    /// </summary>
    public Guid CustomerId { get; set; }

    /// <summary>
    /// Channel the conversation belongs to. Example: 018f4f70-8b5f-7b4c-9d1a-0f6c1d7a2b31.
    /// </summary>
    public Guid CompanyChannelId { get; set; }

    /// <summary>
    /// UTC timestamp of the most recent message in the conversation. Example: 2026-06-07T10:15:30Z.
    /// </summary>
    public DateTime LastMessageAt { get; set; }

    /// <summary>
    /// Handoff ticket identifier from the last request_human_handoff execution. Example: 018f4f70...
    /// </summary>
    public string? HandoffTicketId { get; set; }

    /// <summary>
    /// Categorical reason supplied with the handoff request. Example: customer_requested_human.
    /// </summary>
    public string? Reason { get; set; }

    /// <summary>
    /// Estimated time when a human is expected to pick up. Example: 2026-06-07T10:45:30Z.
    /// </summary>
    public DateTimeOffset? EstimatedPickupAt { get; set; }

    /// <summary>
    /// UTC timestamp when the handoff was requested. Example: 2026-06-07T10:15:31Z.
    /// </summary>
    public DateTime? RequestedAt { get; set; }
}
