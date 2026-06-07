namespace CeoAgent.Shared.Calendar;

public sealed record CalendarReservationCancellationResult(
    bool Succeeded,
    string ReservationId,
    string? EventId,
    string? FailureReason)
{
    public static CalendarReservationCancellationResult Cancelled(string reservationId, string eventId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reservationId);
        ArgumentException.ThrowIfNullOrWhiteSpace(eventId);

        return new CalendarReservationCancellationResult(true, reservationId, eventId, null);
    }

    public static CalendarReservationCancellationResult NotOwned(string reservationId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reservationId);

        return new CalendarReservationCancellationResult(false, reservationId, null, "reservation_not_found_or_not_owned");
    }

    public static CalendarReservationCancellationResult Failed(string reservationId, string failureReason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reservationId);
        ArgumentException.ThrowIfNullOrWhiteSpace(failureReason);

        return new CalendarReservationCancellationResult(false, reservationId, null, failureReason);
    }
}
