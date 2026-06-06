using System.Net;
using System.Security.Cryptography;
using System.Text;
using CeoAgent.Application.Abstractions.AITools.GoogleCalendar;
using CeoAgent.Shared.Calendar;
using Google;
using Google.Apis.Calendar.v3;
using Google.Apis.Calendar.v3.Data;

namespace CeoAgent.Infrastructure.Implementation.AITools.GoogleCalendar.Integration;

/// <summary>
/// Implements calendar availability and reservation operations against Google Calendar.
/// </summary>
public sealed class GoogleCalendarIntegration(IGoogleCalendarServiceFactory googleCalendarServiceFactory)
    : ICalendarIntegration
{
    private const string IdempotencyPropertyName = "ceoagent_idempotency_key";

    /// <summary>
    /// Checks whether the requested interval is free and returns the nearest configured alternative when it is busy.
    /// </summary>
    public async Task<CalendarAvailabilityResult> CheckAvailabilityAsync(
        CalendarAvailabilityRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var service = await googleCalendarServiceFactory.CreateAsync(request.CredentialReference, cancellationToken);
        var duration = request.End - request.Start;
        var allStarts = new[] { request.Start }
            .Concat(request.AlternativeSearchStarts)
            .ToArray();
        var queryStart = allStarts.Min().AddMinutes(-request.BufferMinutes);
        var queryEnd = allStarts.Max().Add(duration).AddMinutes(request.BufferMinutes);
        var busyRanges = await GetBusyRangesAsync(service, request.CalendarId, queryStart, queryEnd, cancellationToken);

        if (busyRanges is null)
        {
            return new CalendarAvailabilityResult(
                Available: false,
                AlternativeStarts: [],
                UnavailabilityReason: "slot_unavailable");
        }

        var primaryAvailable = IsAvailable(busyRanges, request.Start, request.End, request.BufferMinutes);

        if (primaryAvailable)
        {
            return new CalendarAvailabilityResult(Available: true, [], UnavailabilityReason: null);
        }

        var alternatives = new List<DateTimeOffset>();

        foreach (var alternativeStart in request.AlternativeSearchStarts)
        {
            var alternativeEnd = alternativeStart + duration;
            if (IsAvailable(busyRanges, alternativeStart, alternativeEnd, request.BufferMinutes))
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

        var service = await googleCalendarServiceFactory.CreateAsync(request.CredentialReference, cancellationToken);
        var existing = await FindExistingReservationAsync(service, request, cancellationToken);
        if (existing is not null)
        {
            return existing;
        }

        Event created;
        try
        {
            created = await service.Events.Insert(
                BuildEvent(request),
                request.CalendarId).ExecuteAsync(cancellationToken);
        }
        catch (GoogleApiException exception) when (exception.HttpStatusCode == HttpStatusCode.Conflict)
        {
            existing = await FindExistingReservationAsync(service, request, cancellationToken);
            if (existing is not null)
            {
                return existing;
            }

            throw;
        }

        return new CalendarReservationResult(created.Id, created.HtmlLink);
    }

    /// <summary>
    /// Looks up an existing event by the private idempotency property so retrying a reservation does not create a duplicate event.
    /// </summary>
    private static async Task<CalendarReservationResult?> FindExistingReservationAsync(
        CalendarService service,
        CalendarReservationRequest request,
        CancellationToken cancellationToken)
    {
        var existingRequest = service.Events.List(request.CalendarId);
        existingRequest.PrivateExtendedProperty = $"{IdempotencyPropertyName}={request.IdempotencyKey}";
        existingRequest.SingleEvents = true;
        existingRequest.MaxResults = 1;

        var existingEvents = await existingRequest.ExecuteAsync(cancellationToken);
        var existing = existingEvents.Items?
            .FirstOrDefault(item => !string.IsNullOrWhiteSpace(item.Id)
                && !string.IsNullOrWhiteSpace(item.HtmlLink));
        return existing is null
            ? null
            : new CalendarReservationResult(existing.Id, existing.HtmlLink);
    }

    /// <summary>
    /// Builds the Google Calendar event payload from the reservation request, including attendee details and private idempotency metadata.
    /// </summary>
    private static Event BuildEvent(CalendarReservationRequest request)
    {
        return new Event
        {
            Id = BuildDeterministicEventId(request.IdempotencyKey),
            Summary = request.Summary,
            Description = request.Description,
            Start = new EventDateTime
            {
                DateTimeDateTimeOffset = request.Start,
            },
            End = new EventDateTime
            {
                DateTimeDateTimeOffset = request.End,
            },
            Attendees = string.IsNullOrWhiteSpace(request.CustomerEmail)
                ? null
                :
                [
                    new EventAttendee
                    {
                        Email = request.CustomerEmail,
                    },
                ],
            ExtendedProperties = new Event.ExtendedPropertiesData
            {
                Private__ = new Dictionary<string, string>
                {
                    [IdempotencyPropertyName] = request.IdempotencyKey,
                },
            },
        };
    }

    /// <summary>
    /// Creates a stable Google Calendar event id from the reservation idempotency key.
    /// </summary>
    private static string BuildDeterministicEventId(string idempotencyKey)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(idempotencyKey));
        return "ceoagent" + Convert.ToHexString(hash).ToLowerInvariant();
    }

    /// <summary>
    /// Queries Google Calendar free/busy data for the calendar and returns its busy periods, or null when the calendar is missing from the response.
    /// </summary>
    private static async Task<IReadOnlyList<TimePeriod>?> GetBusyRangesAsync(
        CalendarService service,
        string calendarId,
        DateTimeOffset start,
        DateTimeOffset end,
        CancellationToken cancellationToken)
    {
        var response = await service.Freebusy.Query(new FreeBusyRequest
        {
            TimeMinDateTimeOffset = start,
            TimeMaxDateTimeOffset = end,
            Items =
            [
                new FreeBusyRequestItem
                {
                    Id = calendarId,
                },
            ],
        }).ExecuteAsync(cancellationToken);

        if (response.Calendars is null || !response.Calendars.TryGetValue(calendarId, out var calendar))
        {
            return null;
        }

        return calendar.Busy?.ToArray() ?? [];
    }

    /// <summary>
    /// Determines whether an interval, expanded by the configured buffer, avoids all returned busy periods.
    /// </summary>
    private static bool IsAvailable(
        IReadOnlyList<TimePeriod> busyRanges,
        DateTimeOffset start,
        DateTimeOffset end,
        int bufferMinutes)
    {
        var bufferedStart = start.AddMinutes(-bufferMinutes);
        var bufferedEnd = end.AddMinutes(bufferMinutes);
        return busyRanges.All(busy =>
            busy.StartDateTimeOffset >= bufferedEnd
            || busy.EndDateTimeOffset <= bufferedStart);
    }
}
