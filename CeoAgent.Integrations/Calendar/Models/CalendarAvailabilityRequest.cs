namespace CeoAgent.Integrations.Calendar;

public sealed record CalendarAvailabilityRequest(
    string CredentialReference,
    string CalendarId,
    DateTimeOffset Start,
    DateTimeOffset End,
    int PartySize,
    IReadOnlyList<DateTimeOffset> AlternativeSearchStarts,
    int BufferMinutes = 0);
