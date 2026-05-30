namespace CeoAgent.Integrations.Calendar;

public sealed record CalendarReservationRequest(
    string CredentialReference,
    string CalendarId,
    DateTimeOffset Start,
    DateTimeOffset End,
    string Summary,
    string IdempotencyKey,
    string? Description);
