using System.ComponentModel;

namespace CeoAgent.Shared.Request.GoogleCalendar;

public sealed class CheckGoogleCalendarAvailabilityRequest
{
    /// <summary>
    /// Local company date to check for calendar availability.
    /// </summary>
    [Description("Local company date to check for calendar availability.")]
    public DateOnly Date { get; set; }

    /// <summary>
    /// Requested party size.
    /// </summary>
    [Description("Requested party size.")]
    public int PartySize { get; set; }

    /// <summary>
    /// Optional preferred local company time.
    /// </summary>
    [Description("Optional preferred local company time.")]
    public TimeOnly? PreferredTime { get; set; }
}
