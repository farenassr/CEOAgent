using Google.Apis.Calendar.v3;

namespace CeoAgent.Application.Abstractions.AITools.GoogleCalendar;

public interface IGoogleCalendarServiceFactory
{
    Task<CalendarService> CreateAsync(
        string credentialReference,
        CancellationToken cancellationToken);
}
