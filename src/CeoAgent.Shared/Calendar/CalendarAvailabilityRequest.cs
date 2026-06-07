namespace CeoAgent.Shared.Calendar;

public sealed record CalendarAvailabilityRequest(
    string CredentialReference,
    string CalendarId,
    DateTimeOffset Start,
    DateTimeOffset End,
    DateTimeOffset SearchWindowStart,
    DateTimeOffset SearchWindowEnd,
    int PartySize,
    IReadOnlyList<DateTimeOffset> AlternativeSearchStarts,
    bool RequestedSlotEligible = true,
    int BufferMinutes = 0);
