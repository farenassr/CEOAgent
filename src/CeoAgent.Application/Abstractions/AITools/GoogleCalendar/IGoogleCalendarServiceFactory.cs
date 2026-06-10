namespace CeoAgent.Application.Abstractions.AITools.GoogleCalendar;

public interface IGoogleCalendarServiceFactory<TService>
{
    Task<TService> CreateAsync(
        string credentialReference,
        CancellationToken cancellationToken);
}
