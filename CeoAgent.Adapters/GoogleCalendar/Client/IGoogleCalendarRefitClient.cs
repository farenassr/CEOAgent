using Refit;
using CeoAgent.Adapters.GoogleCalendar.Abstractions;

namespace CeoAgent.Adapters.GoogleCalendar.Client;

public interface IGoogleCalendarRefitClient
{
    [Post("/calendar/v3/freeBusy")]
    Task<GoogleFreeBusyResponse> QueryFreeBusyAsync(
        [Body] GoogleFreeBusyRequest request,
        CancellationToken cancellationToken);

    [Post("/calendar/v3/calendars/{calendarId}/events")]
    Task<GoogleCalendarEventResponse> CreateEventAsync(
        string calendarId,
        [Body] GoogleCalendarEventRequest request,
        CancellationToken cancellationToken);
}
