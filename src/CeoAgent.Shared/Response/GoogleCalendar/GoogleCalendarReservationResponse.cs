namespace CeoAgent.Shared.Response.GoogleCalendar;

public sealed class GoogleCalendarReservationResponse
{
    /// <summary>
    /// External Google Calendar event identifier.
    /// </summary>
    public string EventId { get; set; } = string.Empty;

    /// <summary>
    /// URL for the created or previously-created Google Calendar event.
    /// </summary>
    public string EventUrl { get; set; } = string.Empty;
}
