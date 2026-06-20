namespace CeoAgent.Shared.Calendar;

public sealed record CalendarReservationInfo(
    string ReservationId,
    string EventId,
    DateTimeOffset Start,
    DateTimeOffset End,
    string? Summary,
    string? CustomerName,
    string? EventUrl,
    string? CustomerPhoneNumber = null);

