using CeoAgent.Shared.Enums;

namespace CeoAgent.Infrastructure.Entities;

public sealed class OutgoingMessageOutbox : AuditableOrganizationOwnedEntity
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid ConversationId { get; set; }

    public Guid MessageId { get; set; }

    public required string Provider { get; set; }

    public OutgoingMessageOutboxStatus Status { get; set; } = OutgoingMessageOutboxStatus.WaitingToSendToProvider;

    public required string IdempotencyKey { get; set; }

    public string? ProviderMessageId { get; set; }

    public int AttemptCount { get; set; }

    public int MaxAttempts { get; set; } = 5;

    public DateTime? NextAttemptAt { get; set; }

    public DateTime? ClaimedAt { get; set; }

    public string? ClaimedBy { get; set; }

    public string? CorrelationId { get; set; }

    public string? LastError { get; set; }

    public DateTime? SentAt { get; set; }

    public DateTime? CompletedAt { get; set; }

    public Conversation Conversation { get; set; } = null!;

    public Message Message { get; set; } = null!;

    public ICollection<ProviderSendLedger> ProviderSendLedgers { get; } = [];
}
