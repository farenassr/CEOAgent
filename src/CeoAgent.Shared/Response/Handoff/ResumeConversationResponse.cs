using CeoAgent.Shared.Enums;

namespace CeoAgent.Shared.Response.Handoff;

/// <summary>
/// Result of an explicit admin resume of a handed-off conversation.
/// </summary>
public sealed class ResumeConversationResponse
{
    /// <summary>
    /// Conversation that was resumed. Example: 018f4f70-8b5f-7b4c-9d1a-0f6c1d7a2b34.
    /// </summary>
    public Guid ConversationId { get; set; }

    /// <summary>
    /// Conversation status after the resume request. Example: Open.
    /// </summary>
    public ConversationStatus Status { get; set; }

    /// <summary>
    /// Whether this request transitioned the conversation from HandedOff back to Open. Example: true.
    /// </summary>
    public bool Resumed { get; set; }
}
