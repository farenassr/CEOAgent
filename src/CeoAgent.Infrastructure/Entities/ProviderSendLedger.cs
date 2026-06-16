using CeoAgent.Shared.Enums;

namespace CeoAgent.Infrastructure.Entities;

public sealed class ProviderSendLedger : AuditableOrganizationOwnedEntity
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid OutgoingMessageOutboxId { get; set; }

    public int AttemptNumber { get; set; }

    public required string Provider { get; set; }

    public ProviderSendLedgerStatus Status { get; set; } = ProviderSendLedgerStatus.SendAttemptStarted;

    public string? RequestHash { get; set; }

    public string? ProviderMessageId { get; set; }

    public string? ExternalResponseJson { get; set; }

    public string? ErrorCode { get; set; }

    public string? ErrorMessage { get; set; }

    public DateTime StartedAt { get; set; }

    public DateTime? FinishedAt { get; set; }

    public string? CorrelationId { get; set; }

    public OutgoingMessageOutbox OutgoingMessageOutbox { get; set; } = null!;
}
