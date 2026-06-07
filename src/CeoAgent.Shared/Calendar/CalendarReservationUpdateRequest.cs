namespace CeoAgent.Shared.Calendar;

public sealed record CalendarReservationUpdateRequest(
    string CredentialReference,
    string CalendarId,
    string CompanyId,
    string CustomerExternalId,
    string ReservationId,
    DateTimeOffset NewStart,
    DateTimeOffset NewEnd,
    string? Summary,
    string? CustomerName,
    int BufferMinutes = 0);
