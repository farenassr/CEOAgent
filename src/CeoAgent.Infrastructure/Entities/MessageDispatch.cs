using CeoAgent.Shared.Enums;

namespace CeoAgent.Infrastructure.Entities;

public sealed class MessageDispatch : AuditableOrganizationOwnedEntity
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid ConversationId { get; set; }

    public Guid MessageId { get; set; }

    public MessageDispatchOperation Operation { get; set; }

    public required string Provider { get; set; }

    public MessageDispatchStatus Status { get; set; } = MessageDispatchStatus.Pending;

    public required string IdempotencyKey { get; set; }

    public int AttemptCount { get; set; }

    public int MaxAttempts { get; set; } = 5;

    public DateTime? NextAttemptAt { get; set; }

    public DateTime? LastAttemptAt { get; set; }

    public DateTime? ClaimedAt { get; set; }

    public string? ClaimedBy { get; set; }

    public DateTime? SucceededAt { get; set; }

    public string? ProviderMessageId { get; set; }

    public string? LastError { get; set; }

    public string? CorrelationId { get; set; }

    public Conversation Conversation { get; set; } = null!;

    public Message Message { get; set; } = null!;
}
