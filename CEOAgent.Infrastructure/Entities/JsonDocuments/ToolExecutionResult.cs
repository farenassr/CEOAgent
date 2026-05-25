using System.Text.Json.Serialization;

namespace CeoAgent.Infrastructure.Entities.JsonDocuments;

public sealed class ToolExecutionResult
{
    /// <summary>
    /// Tool key that identifies which result variant is active.
    /// </summary>
    public required string ToolKey { get; set; }

    /// <summary>
    /// Result payload for a check availability tool execution.
    /// </summary>
    [JsonPropertyName("check_availability")]
    public CheckAvailabilityResult? CheckAvailability { get; set; }

    /// <summary>
    /// Result payload for a human handoff tool execution.
    /// </summary>
    [JsonPropertyName("request_human_handoff")]
    public RequestHumanHandoffResult? RequestHumanHandoff { get; set; }

    /// <summary>
    /// Result payload for a calendar event creation tool execution.
    /// </summary>
    [JsonPropertyName("create_calendar_event")]
    public CreateCalendarEventResult? CreateCalendarEvent { get; set; }

    public static ToolExecutionResult ForCheckAvailability(CheckAvailabilityResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        return new ToolExecutionResult
        {
            ToolKey = "check_availability",
            CheckAvailability = result,
        };
    }

    public static ToolExecutionResult ForRequestHumanHandoff(RequestHumanHandoffResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        return new ToolExecutionResult
        {
            ToolKey = "request_human_handoff",
            RequestHumanHandoff = result,
        };
    }

    public static ToolExecutionResult ForCreateCalendarEvent(CreateCalendarEventResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        return new ToolExecutionResult
        {
            ToolKey = "create_calendar_event",
            CreateCalendarEvent = result,
        };
    }
}

public sealed class CheckAvailabilityResult
{
    /// <summary>
    /// Whether the requested slot is available.
    /// </summary>
    public bool Available { get; set; }

    /// <summary>
    /// Alternative local times when the requested slot is unavailable.
    /// </summary>
    public List<TimeOnly> AlternativeSlots { get; set; } = [];

    /// <summary>
    /// Optional reason explaining why the requested slot is unavailable.
    /// </summary>
    public string? UnavailabilityReason { get; set; }
}

public sealed class RequestHumanHandoffResult
{
    /// <summary>
    /// Whether a human handoff request was created.
    /// </summary>
    public bool HandoffRequested { get; set; }

    /// <summary>
    /// Identifier for the handoff ticket when one was created.
    /// </summary>
    public string? HandoffTicketId { get; set; }

    /// <summary>
    /// Estimated time when a human is expected to pick up the conversation.
    /// </summary>
    public DateTimeOffset? EstimatedPickupAt { get; set; }
}

public sealed class CreateCalendarEventResult
{
    /// <summary>
    /// External calendar event identifier.
    /// </summary>
    public required string EventId { get; set; }

    /// <summary>
    /// URL for the created calendar event.
    /// </summary>
    public required string EventUrl { get; set; }
}
