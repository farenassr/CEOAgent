namespace CeoAgent.Shared.Calendar;

public sealed record CalendarReservationMutationResult(
    bool Succeeded,
    CalendarReservationInfo? Reservation,
    string? FailureReason)
{
    public static CalendarReservationMutationResult Updated(CalendarReservationInfo reservation)
    {
        ArgumentNullException.ThrowIfNull(reservation);

        return new CalendarReservationMutationResult(true, reservation, null);
    }

    public static CalendarReservationMutationResult NotOwned(string reservationId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reservationId);

        return new CalendarReservationMutationResult(false, null, "reservation_not_found_or_not_owned");
    }

    public static CalendarReservationMutationResult Failed(string failureReason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(failureReason);

        return new CalendarReservationMutationResult(false, null, failureReason);
    }
}
