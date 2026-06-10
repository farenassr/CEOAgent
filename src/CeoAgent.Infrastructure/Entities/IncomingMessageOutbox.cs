namespace CeoAgent.Infrastructure.Entities;

public sealed class IncomingMessageOutbox : AuditableCompanyOwnedEntity
{
    /// <summary>
    /// Unique outbox identifier. Example: 018f4f70-8b5f-7b4c-9d1a-0f6c1d7a2b42.
    /// </summary>
    public Guid Id { get; set; } = Guid.CreateVersion7();

    /// <summary>
    /// Conversation containing the inbound message to process.
    /// </summary>
    public Guid ConversationId { get; set; }

    /// <summary>
    /// Inbound message that should be processed by the Worker.
    /// </summary>
    public Guid MessageId { get; set; }

    /// <summary>
    /// Request correlation ID to flow into the queue job when available.
    /// </summary>
    public string? CorrelationId { get; set; }

    /// <summary>
    /// Current dispatch state for the queue publication.
    /// </summary>
    public IncomingMessageOutboxStatus Status { get; set; } = IncomingMessageOutboxStatus.Pending;

    /// <summary>
    /// Number of queue dispatch attempts made for this row.
    /// </summary>
    public int AttemptCount { get; set; }

    /// <summary>
    /// Last UTC time when a queue dispatch was attempted.
    /// </summary>
    public DateTime? LastAttemptAt { get; set; }

    /// <summary>
    /// UTC time when the row was successfully dispatched to the queue.
    /// </summary>
    public DateTime? DispatchedAt { get; set; }

    /// <summary>
    /// Bounded failure summary from the last failed dispatch attempt.
    /// </summary>
    public string? FailureReason { get; set; }

    /// <summary>
    /// Conversation containing the inbound message.
    /// </summary>
    public Conversation Conversation { get; set; } = null!;

    /// <summary>
    /// Inbound message that should be processed.
    /// </summary>
    public Message Message { get; set; } = null!;
}

public enum IncomingMessageOutboxStatus
{
    Pending,
    Failed,
    Dispatched,
}
