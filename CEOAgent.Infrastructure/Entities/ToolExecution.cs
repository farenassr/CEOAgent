using CeoAgent.Shared.Enums;
using CeoAgent.Infrastructure.Entities.JsonDocuments;

namespace CeoAgent.Infrastructure.Entities;

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
    /// Enabled company tool used for this execution. Example: 018f4f70-8b5f-7b4c-9d1a-0f6c1d7a2b40.
    /// </summary>
    public Guid CompanyToolId { get; set; }

    /// <summary>
    /// Assistant message that requested this tool call. Example: 018f4f70-8b5f-7b4c-9d1a-0f6c1d7a2b36.
    /// </summary>
    public Guid TriggerMessageId { get; set; }

    /// <summary>
    /// Message carrying the tool result back to the conversation. Example: 018f4f70-8b5f-7b4c-9d1a-0f6c1d7a2b37.
    /// </summary>
    public Guid? ResultMessageId { get; set; }

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
    /// Structured tool request payload stored as JSON. Example: {"toolKey":"check_availability","date":"2026-05-22","partySize":4}.
    /// </summary>
    public ToolExecutionRequest? Request { get; set; }

    /// <summary>
    /// Structured tool result payload stored as JSON. Example: {"toolKey":"request_human_handoff","handoffRequested":true}.
    /// </summary>
    public ToolExecutionResult? Result { get; set; }

    /// <summary>
    /// Failure reason when execution did not succeed. Example: capacity_unavailable.
    /// </summary>
    public string? FailureReason { get; set; }

    /// <summary>
    /// Conversation where this tool execution was requested. Example: the active customer conversation.
    /// </summary>
    public Conversation Conversation { get; set; } = null!;

    /// <summary>
    /// Enabled company tool used for this execution. Example: request_human_handoff registration.
    /// </summary>
    public CompanyTool CompanyTool { get; set; } = null!;

    /// <summary>
    /// Assistant message that triggered this execution. Example: assistant tool-call message.
    /// </summary>
    public Message TriggerMessage { get; set; } = null!;

    /// <summary>
    /// Optional message containing this execution's result. Example: tool result message.
    /// </summary>
    public Message? ResultMessage { get; set; }
}
