namespace CeoAgent.Shared.Calendar;

public sealed record CalendarReservationSearchResult(
    IReadOnlyList<CalendarReservationInfo> Reservations,
    string? FailureReason = null)
{
    public static CalendarReservationSearchResult Failed(string failureReason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(failureReason);

        return new CalendarReservationSearchResult([], failureReason);
    }
}
