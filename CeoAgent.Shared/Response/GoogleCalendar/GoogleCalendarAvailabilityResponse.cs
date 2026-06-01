namespace CeoAgent.Shared.Response.GoogleCalendar;

public sealed class GoogleCalendarAvailabilityResponse
{
    /// <summary>
    /// Whether the requested slot is available.
    /// </summary>
    public bool Available { get; set; }

    /// <summary>
    /// Alternative local company times when the requested slot is unavailable.
    /// </summary>
    public List<TimeOnly> AlternativeSlots { get; set; } = [];

    /// <summary>
    /// Optional reason explaining why the requested slot is unavailable.
    /// </summary>
    public string? UnavailabilityReason { get; set; }
}
