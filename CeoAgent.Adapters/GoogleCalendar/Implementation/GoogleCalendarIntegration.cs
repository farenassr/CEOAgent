using CeoAgent.Adapters.GoogleCalendar.Abstractions;
using CeoAgent.Adapters.GoogleCalendar.Client;
using CeoAgent.Integrations.Calendar;

namespace CeoAgent.Adapters.GoogleCalendar;

/// <summary>
/// Implements calendar availability and reservation operations against Google Calendar.
/// </summary>
public sealed class GoogleCalendarIntegration(IGoogleCalendarRefitClientFactory googleCalendarClientFactory)
    : ICalendarIntegration
{
    /// <summary>
    /// Checks whether the requested interval is free and returns the nearest configured alternative when it is busy.
    /// </summary>
    public async Task<CalendarAvailabilityResult> CheckAvailabilityAsync(
        CalendarAvailabilityRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var client = await googleCalendarClientFactory.CreateAsync(request.CredentialReference, cancellationToken);
        var duration = request.End - request.Start;
        var allStarts = new[] { request.Start }
            .Concat(request.AlternativeSearchStarts)
            .ToArray();
        var queryStart = allStarts.Min();
        var queryEnd = allStarts.Max().Add(duration);
        var busyRanges = await GetBusyRangesAsync(client, request.CalendarId, queryStart, queryEnd, cancellationToken);
        if (busyRanges is null)
        {
            return new CalendarAvailabilityResult(
                Available: false,
                AlternativeStarts: [],
                UnavailabilityReason: "slot_unavailable");
        }

        var primaryAvailable = IsAvailable(busyRanges, request.Start, request.End);
        if (primaryAvailable)
        {
            return new CalendarAvailabilityResult(Available: true, [], UnavailabilityReason: null);
        }

        var alternatives = new List<DateTimeOffset>();

        foreach (var alternativeStart in request.AlternativeSearchStarts)
        {
            var alternativeEnd = alternativeStart + duration;
            if (IsAvailable(busyRanges, alternativeStart, alternativeEnd))
            {
                alternatives.Add(alternativeStart);
                break;
            }
        }

        return new CalendarAvailabilityResult(
            Available: false,
            AlternativeStarts: alternatives,
            UnavailabilityReason: "slot_unavailable");
    }

    /// <summary>
    /// Creates a Google Calendar event with the tool idempotency key stored in extended properties.
    /// </summary>
    public async Task<CalendarReservationResult> CreateReservationAsync(
        CalendarReservationRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var client = await googleCalendarClientFactory.CreateAsync(request.CredentialReference, cancellationToken);
        var created = await client.CreateEventAsync(
            request.CalendarId,
            new GoogleCalendarEventRequest(
                request.Summary,
                request.Description,
                new GoogleCalendarEventDateTime(request.Start),
                new GoogleCalendarEventDateTime(request.End),
                new GoogleExtendedProperties(new Dictionary<string, string>
                {
                    ["ceoagent_idempotency_key"] = request.IdempotencyKey,
                })),
            cancellationToken);

        return new CalendarReservationResult(created.Id, created.HtmlLink);
    }

    private static async Task<IReadOnlyList<GoogleBusyRange>?> GetBusyRangesAsync(
        IGoogleCalendarRefitClient client,
        string calendarId,
        DateTimeOffset start,
        DateTimeOffset end,
        CancellationToken cancellationToken)
    {
        var response = await client.QueryFreeBusyAsync(
            new GoogleFreeBusyRequest(
                start,
                end,
                [new GoogleFreeBusyItem(calendarId)]),
            cancellationToken);

        if (response.Calendars is null || !response.Calendars.TryGetValue(calendarId, out var calendar))
        {
            return null;
        }

        return calendar.Busy ?? [];
    }

    private static bool IsAvailable(
        IReadOnlyList<GoogleBusyRange> busyRanges,
        DateTimeOffset start,
        DateTimeOffset end)
    {
        return busyRanges.All(busy => busy.Start >= end || busy.End <= start);
    }
}
