using CeoAgent.Shared.Calendar;

namespace CeoAgent.Application.Abstractions.AITools.GoogleCalendar;

public interface IGoogleCalendarIntegration
{
    Task<CalendarAvailabilityResult> CheckAvailabilityAsync(
        CalendarAvailabilityRequest request,
        CancellationToken cancellationToken);

    Task<CalendarReservationResult> CreateReservationAsync(
        CalendarReservationRequest request,
        CancellationToken cancellationToken);

    Task<CalendarReservationSearchResult> FindReservationsAsync(
        CalendarReservationSearchRequest request,
        CancellationToken cancellationToken);

    Task<CalendarReservationMutationResult> UpdateReservationAsync(
        CalendarReservationUpdateRequest request,
        CancellationToken cancellationToken);

    Task<CalendarReservationCancellationResult> CancelReservationAsync(
        CalendarReservationCancellationRequest request,
        CancellationToken cancellationToken);
}
