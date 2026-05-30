namespace CeoAgent.Integrations.Calendar;

public sealed record CalendarAvailabilityResult(
    bool Available,
    IReadOnlyList<DateTimeOffset> AlternativeStarts,
    string? UnavailabilityReason);
