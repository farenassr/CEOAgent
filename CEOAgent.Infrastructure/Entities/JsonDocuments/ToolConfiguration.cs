using System.Text.Json.Serialization;

namespace CeoAgent.Infrastructure.Entities.JsonDocuments;

public sealed class ToolConfiguration
{
    /// <summary>
    /// Tool key that identifies which configuration variant is active.
    /// </summary>
    public required string ToolKey { get; set; }

    /// <summary>
    /// Configuration for the check availability tool.
    /// </summary>
    [JsonPropertyName("check_availability")]
    public CheckAvailabilityConfig? CheckAvailability { get; set; }

    /// <summary>
    /// Configuration for the request human handoff tool.
    /// </summary>
    [JsonPropertyName("request_human_handoff")]
    public RequestHumanHandoffConfig? RequestHumanHandoff { get; set; }

    /// <summary>
    /// Configuration for Google Calendar-backed tools.
    /// </summary>
    [JsonPropertyName("google_calendar")]
    public GoogleCalendarConfig? GoogleCalendar { get; set; }

    public static ToolConfiguration ForCheckAvailability(CheckAvailabilityConfig configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        return new ToolConfiguration
        {
            ToolKey = "check_availability",
            CheckAvailability = configuration,
        };
    }

    public static ToolConfiguration ForRequestHumanHandoff(RequestHumanHandoffConfig configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        return new ToolConfiguration
        {
            ToolKey = "request_human_handoff",
            RequestHumanHandoff = configuration,
        };
    }

    public static ToolConfiguration ForGoogleCalendar(GoogleCalendarConfig configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        return new ToolConfiguration
        {
            ToolKey = "google_calendar",
            GoogleCalendar = configuration,
        };
    }
}

public sealed class CheckAvailabilityConfig
{
    /// <summary>
    /// Maximum supported party size for availability checks.
    /// </summary>
    public int MaxPartySize { get; set; }

    /// <summary>
    /// Minimum supported party size for availability checks.
    /// </summary>
    public int MinPartySize { get; set; }

    /// <summary>
    /// Slot granularity in minutes.
    /// </summary>
    public int SlotMinutes { get; set; }

    /// <summary>
    /// Maximum number of days in advance that can be checked.
    /// </summary>
    public int AdvanceBookingDays { get; set; }
}

public sealed class RequestHumanHandoffConfig
{
    /// <summary>
    /// Channel or queue where human handoff requests should be escalated.
    /// </summary>
    public string? EscalationChannel { get; set; }

    /// <summary>
    /// User identifiers to notify when handoff is requested.
    /// </summary>
    public List<string> NotifyUsers { get; set; } = [];

    /// <summary>
    /// Handoff timeout in minutes.
    /// </summary>
    public int TimeoutMinutes { get; set; }
}

public sealed class GoogleCalendarConfig
{
    /// <summary>
    /// Calendar identifier used by Google Calendar tools.
    /// </summary>
    public required string CalendarId { get; set; }

    /// <summary>
    /// Time zone identifier used for calendar operations.
    /// </summary>
    public required string TimeZoneId { get; set; }

    /// <summary>
    /// Buffer in minutes applied around calendar events.
    /// </summary>
    public int BufferMinutes { get; set; }

    /// <summary>
    /// Reservation duration in minutes.
    /// </summary>
    public int ReservationMinutes { get; set; } = GoogleCalendarSchedulingDefaults.ReservationMinutes;

    /// <summary>
    /// Maximum number of days in advance that can be booked.
    /// </summary>
    public int AdvanceBookingDays { get; set; } = GoogleCalendarSchedulingDefaults.AdvanceBookingDays;

    /// <summary>
    /// Slot granularity in minutes.
    /// </summary>
    public int SlotMinutes { get; set; } = GoogleCalendarSchedulingDefaults.SlotMinutes;
}

public static class GoogleCalendarSchedulingDefaults
{
    public const int ReservationMinutes = 60;

    public const int AdvanceBookingDays = 14;

    public const int SlotMinutes = 30;
}
