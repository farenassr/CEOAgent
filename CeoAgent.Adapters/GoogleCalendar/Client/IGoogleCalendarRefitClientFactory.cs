namespace CeoAgent.Adapters.GoogleCalendar.Client;

public interface IGoogleCalendarRefitClientFactory
{
    Task<IGoogleCalendarRefitClient> CreateAsync(
        string credentialReference,
        CancellationToken cancellationToken);
}
