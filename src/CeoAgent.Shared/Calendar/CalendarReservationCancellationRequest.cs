namespace CeoAgent.Shared.Calendar;

public sealed record CalendarReservationCancellationRequest(
    string CredentialReference,
    string CalendarId,
    string CompanyId,
    string CustomerExternalId,
    string ReservationId,
    string? Reason);

