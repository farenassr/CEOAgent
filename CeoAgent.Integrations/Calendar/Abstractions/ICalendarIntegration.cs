namespace CeoAgent.Integrations.Calendar;

public interface ICalendarIntegration
{
    Task<CalendarAvailabilityResult> CheckAvailabilityAsync(
        CalendarAvailabilityRequest request,
        CancellationToken cancellationToken);

    Task<CalendarReservationResult> CreateReservationAsync(
        CalendarReservationRequest request,
        CancellationToken cancellationToken);
}
