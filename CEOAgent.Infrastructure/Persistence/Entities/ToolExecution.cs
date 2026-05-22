using CEOAgent.Shared.Enums;

namespace CEOAgent.Infrastructure.Persistence.Entities;

public sealed class ToolExecution : AuditableCompanyOwnedEntity
{
    /// <summary>
    /// Unique tool execution identifier. Example: 018f4f70-8b5f-7b4c-9d1a-0f6c1d7a2b39.
    /// </summary>
    public Guid Id { get; set; } = Guid.CreateVersion7();

    /// <summary>
    /// Conversation where the tool execution was requested. Example: 018f4f70-8b5f-7b4c-9d1a-0f6c1d7a2b34.
    /// </summary>
    public Guid ConversationId { get; set; }

    /// <summary>
    /// Tool key requested by the agent. Example: request_human_handoff.
    /// </summary>
    public required string ToolKey { get; set; }

    /// <summary>
    /// Stable key used to make tool execution idempotent. Example: conversation-123:request_human_handoff:1.
    /// </summary>
    public required string IdempotencyKey { get; set; }

    /// <summary>
    /// Final execution status for the tool request. Example: Succeeded.
    /// </summary>
    public ToolExecutionStatus Status { get; set; }

    /// <summary>
    /// Structured tool request payload stored as JSON. Example: {"date":"2026-05-22","partySize":4}.
    /// </summary>
    public string? RequestJson { get; set; }

    /// <summary>
    /// Structured tool result payload stored as JSON. Example: {"handoffRequested":true}.
    /// </summary>
    public string? ResultJson { get; set; }

    /// <summary>
    /// Failure reason when execution did not succeed. Example: capacity_unavailable.
    /// </summary>
    public string? FailureReason { get; set; }
}
