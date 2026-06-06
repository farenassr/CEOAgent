using System.ComponentModel;

namespace CeoAgent.Shared.Request.GoogleCalendar;

public sealed class CreateGoogleCalendarReservationRequest
{
    /// <summary>
    /// Reservation start timestamp.
    /// </summary>
    [Description("Reservation start timestamp.")]
    public DateTimeOffset Start { get; set; }

    /// <summary>
    /// Reservation end timestamp.
    /// </summary>
    [Description("Reservation end timestamp.")]
    public DateTimeOffset End { get; set; }

    /// <summary>
    /// Calendar event summary.
    /// </summary>
    [Description("Calendar event summary.")]
    public string Summary { get; set; } = string.Empty;

    /// <summary>
    /// Optional calendar event description.
    /// </summary>
    [Description("Optional calendar event description.")]
    public string? Description { get; set; }

    /// <summary>
    /// Customer full name, when captured from the booking form.
    /// </summary>
    [Description("Customer full name, when captured from the booking form.")]
    public string? CustomerName { get; set; }

    /// <summary>
    /// Customer email, optionally added as an event attendee.
    /// </summary>
    [Description("Customer email, optionally added as an event attendee.")]
    public string? CustomerEmail { get; set; }

    /// <summary>
    /// Optional customer notes.
    /// </summary>
    [Description("Optional customer notes.")]
    public string? Notes { get; set; }

    /// <summary>
    /// Client-provided idempotency key stored on the calendar event.
    /// </summary>
    [Description("Client-provided idempotency key stored on the calendar event.")]
    public string IdempotencyKey { get; set; } = string.Empty;
}
