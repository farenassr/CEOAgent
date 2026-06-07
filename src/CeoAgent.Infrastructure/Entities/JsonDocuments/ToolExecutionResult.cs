using System.Text.Json.Serialization;
using CeoAgent.Shared.Constants;

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

    /// <summary>
    /// Result payload for finding Google Calendar reservations.
    /// </summary>
    [JsonPropertyName("find_google_calendar_reservations")]
    public FindGoogleCalendarReservationsResult? FindGoogleCalendarReservations { get; set; }

    /// <summary>
    /// Result payload for updating a Google Calendar reservation.
    /// </summary>
    [JsonPropertyName("update_google_calendar_reservation")]
    public UpdateGoogleCalendarReservationResult? UpdateGoogleCalendarReservation { get; set; }

    /// <summary>
    /// Result payload for cancelling a Google Calendar reservation.
    /// </summary>
    [JsonPropertyName("cancel_google_calendar_reservation")]
    public CancelGoogleCalendarReservationResult? CancelGoogleCalendarReservation { get; set; }

    public static ToolExecutionResult ForCheckAvailability(CheckAvailabilityResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        return new ToolExecutionResult
        {
            ToolKey = "check_availability",
            CheckAvailability = result,
        };
    }

    public static ToolExecutionResult ForCheckGoogleCalendarAvailability(CheckAvailabilityResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        return new ToolExecutionResult
        {
            ToolKey = MvpToolKeys.CheckGoogleCalendarAvailability,
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

    public static ToolExecutionResult ForCreateGoogleCalendarReservation(CreateCalendarEventResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        return new ToolExecutionResult
        {
            ToolKey = MvpToolKeys.CreateGoogleCalendarReservation,
            CreateCalendarEvent = result,
        };
    }

    public static ToolExecutionResult ForFindGoogleCalendarReservations(FindGoogleCalendarReservationsResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        return new ToolExecutionResult
        {
            ToolKey = MvpToolKeys.FindGoogleCalendarReservations,
            FindGoogleCalendarReservations = result,
        };
    }

    public static ToolExecutionResult ForUpdateGoogleCalendarReservation(UpdateGoogleCalendarReservationResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        return new ToolExecutionResult
        {
            ToolKey = MvpToolKeys.UpdateGoogleCalendarReservation,
            UpdateGoogleCalendarReservation = result,
        };
    }

    public static ToolExecutionResult ForCancelGoogleCalendarReservation(CancelGoogleCalendarReservationResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        return new ToolExecutionResult
        {
            ToolKey = MvpToolKeys.CancelGoogleCalendarReservation,
            CancelGoogleCalendarReservation = result,
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

public sealed class FindGoogleCalendarReservationsResult
{
    /// <summary>
    /// Matching reservations safe to show to the model.
    /// </summary>
    public List<GoogleCalendarReservationResultItem> Reservations { get; set; } = [];

    /// <summary>
    /// Number of reservations returned.
    /// </summary>
    public int Count { get; set; }

    /// <summary>
    /// Whether the model must ask the customer which reservation to modify.
    /// </summary>
    public bool DisambiguationNeeded { get; set; }
}

public sealed class UpdateGoogleCalendarReservationResult
{
    /// <summary>
    /// Updated reservation details.
    /// </summary>
    public GoogleCalendarReservationResultItem? Reservation { get; set; }
}

public sealed class CancelGoogleCalendarReservationResult
{
    /// <summary>
    /// Whether the reservation was cancelled.
    /// </summary>
    public bool Cancelled { get; set; }

    /// <summary>
    /// Reservation identifier that was cancelled.
    /// </summary>
    public required string ReservationId { get; set; }

    /// <summary>
    /// External calendar event identifier that was cancelled.
    /// </summary>
    public string? EventId { get; set; }
}

public sealed class GoogleCalendarReservationResultItem
{
    /// <summary>
    /// Stable reservation identifier.
    /// </summary>
    public required string ReservationId { get; set; }

    /// <summary>
    /// External calendar event identifier.
    /// </summary>
    public required string EventId { get; set; }

    /// <summary>
    /// Reservation start in company-local offset.
    /// </summary>
    public DateTimeOffset Start { get; set; }

    /// <summary>
    /// Reservation end in company-local offset.
    /// </summary>
    public DateTimeOffset End { get; set; }

    /// <summary>
    /// Event summary.
    /// </summary>
    public string? Summary { get; set; }

    /// <summary>
    /// Customer name when available from event data.
    /// </summary>
    public string? CustomerName { get; set; }

    /// <summary>
    /// Calendar event URL when safe to return.
    /// </summary>
    public string? EventUrl { get; set; }
}
