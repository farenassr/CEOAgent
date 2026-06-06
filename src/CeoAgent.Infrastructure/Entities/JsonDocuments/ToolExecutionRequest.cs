using System.Text.Json.Serialization;
using CeoAgent.Shared.Constants;

namespace CeoAgent.Infrastructure.Entities.JsonDocuments;

public sealed class ToolExecutionRequest
{
    /// <summary>
    /// Tool key that identifies which request variant is active.
    /// </summary>
    public required string ToolKey { get; set; }

    /// <summary>
    /// Request payload for a check availability tool execution.
    /// </summary>
    [JsonPropertyName("check_availability")]
    public CheckAvailabilityRequest? CheckAvailability { get; set; }

    /// <summary>
    /// Request payload for a human handoff tool execution.
    /// </summary>
    [JsonPropertyName("request_human_handoff")]
    public RequestHumanHandoffRequest? RequestHumanHandoff { get; set; }

    /// <summary>
    /// Request payload for a calendar event creation tool execution.
    /// </summary>
    [JsonPropertyName("create_calendar_event")]
    public CreateCalendarEventRequest? CreateCalendarEvent { get; set; }

    public static ToolExecutionRequest ForCheckAvailability(CheckAvailabilityRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        return new ToolExecutionRequest
        {
            ToolKey = "check_availability",
            CheckAvailability = request,
        };
    }

    public static ToolExecutionRequest ForCheckGoogleCalendarAvailability(CheckAvailabilityRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        return new ToolExecutionRequest
        {
            ToolKey = MvpToolKeys.CheckGoogleCalendarAvailability,
            CheckAvailability = request,
        };
    }

    public static ToolExecutionRequest ForRequestHumanHandoff(RequestHumanHandoffRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        return new ToolExecutionRequest
        {
            ToolKey = "request_human_handoff",
            RequestHumanHandoff = request,
        };
    }

    public static ToolExecutionRequest ForCreateCalendarEvent(CreateCalendarEventRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        return new ToolExecutionRequest
        {
            ToolKey = "create_calendar_event",
            CreateCalendarEvent = request,
        };
    }

    public static ToolExecutionRequest ForCreateGoogleCalendarReservation(CreateCalendarEventRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        return new ToolExecutionRequest
        {
            ToolKey = MvpToolKeys.CreateGoogleCalendarReservation,
            CreateCalendarEvent = request,
        };
    }
}

public sealed class CheckAvailabilityRequest
{
    /// <summary>
    /// Local date to check for availability.
    /// </summary>
    public DateOnly Date { get; set; }

    /// <summary>
    /// Requested party size.
    /// </summary>
    public int PartySize { get; set; }

    /// <summary>
    /// Preferred local time when the customer provided one.
    /// </summary>
    public TimeOnly? PreferredTime { get; set; }
}

public sealed class RequestHumanHandoffRequest
{
    /// <summary>
    /// Reason for requesting human handoff.
    /// </summary>
    public required string Reason { get; set; }

    /// <summary>
    /// Optional notes to include with the handoff request.
    /// </summary>
    public string? Notes { get; set; }
}

public sealed class CreateCalendarEventRequest
{
    /// <summary>
    /// Event start timestamp.
    /// </summary>
    public DateTimeOffset Start { get; set; }

    /// <summary>
    /// Event end timestamp.
    /// </summary>
    public DateTimeOffset End { get; set; }

    /// <summary>
    /// Event summary or title.
    /// </summary>
    public required string Summary { get; set; }
}
