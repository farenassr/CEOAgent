namespace CeoAgent.Shared.Calendar;

public sealed record CalendarReservationSearchRequest(
    string CredentialReference,
    string CalendarId,
    string CompanyId,
    string CustomerExternalId,
    DateTimeOffset TimeMin,
    DateTimeOffset TimeMax,
    bool IncludePast);

