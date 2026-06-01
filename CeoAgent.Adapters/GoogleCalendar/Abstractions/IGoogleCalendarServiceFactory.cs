using Google.Apis.Calendar.v3;

namespace CeoAgent.Adapters.GoogleCalendar.Abstractions;

public interface IGoogleCalendarServiceFactory
{
    Task<CalendarService> CreateAsync(
        string credentialReference,
        CancellationToken cancellationToken);
}
