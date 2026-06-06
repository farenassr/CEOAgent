using CeoAgent.Shared.Calendar;

namespace CeoAgent.Application.Abstractions.AITools.GoogleCalendar;

public interface ICalendarIntegration
{
    Task<CalendarAvailabilityResult> CheckAvailabilityAsync(
        CalendarAvailabilityRequest request,
        CancellationToken cancellationToken);

    Task<CalendarReservationResult> CreateReservationAsync(
        CalendarReservationRequest request,
        CancellationToken cancellationToken);
}
