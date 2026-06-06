namespace CeoAgent.Infrastructure.Entities.JsonDocuments;

public sealed class ConversationStateSnapshot
{
    /// <summary>
    /// Current detected intent for the conversation state.
    /// </summary>
    public string? CurrentIntent { get; set; }

    /// <summary>
    /// Pending action or next expected step for the conversation.
    /// </summary>
    public string? PendingAction { get; set; }

    /// <summary>
    /// Structured slot values captured during the current flow.
    /// </summary>
    public List<ConversationSlot> Slots { get; set; } = [];

    /// <summary>
    /// Flags that annotate conversation state transitions.
    /// </summary>
    public List<string> ConversationFlags { get; set; } = [];

    /// <summary>
    /// Number of turns processed in the active state flow.
    /// </summary>
    public int TurnCount { get; set; }
}

public sealed class ConversationSlot
{
    /// <summary>
    /// Slot name used by the active conversation flow.
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// Text value captured for the slot.
    /// </summary>
    public string? TextValue { get; set; }

    /// <summary>
    /// Numeric value captured for the slot.
    /// </summary>
    public decimal? NumberValue { get; set; }

    /// <summary>
    /// Boolean value captured for the slot.
    /// </summary>
    public bool? BooleanValue { get; set; }

    /// <summary>
    /// Date value captured for the slot.
    /// </summary>
    public DateOnly? DateValue { get; set; }

    /// <summary>
    /// Time value captured for the slot.
    /// </summary>
    public TimeOnly? TimeValue { get; set; }
}
