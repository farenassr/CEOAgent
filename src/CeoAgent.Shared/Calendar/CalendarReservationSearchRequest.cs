namespace CeoAgent.Shared.Calendar;

public sealed record CalendarReservationSearchRequest(
    string CredentialReference,
    string CalendarId,
    string OrganizationId,
    string CustomerExternalId,
    DateTimeOffset TimeMin,
    DateTimeOffset TimeMax,
    bool IncludePast);

