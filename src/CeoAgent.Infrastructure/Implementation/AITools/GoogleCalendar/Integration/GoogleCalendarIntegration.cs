using System.Net;
using System.Security.Cryptography;
using System.Text;
using CeoAgent.Application.Abstractions.AITools.GoogleCalendar;
using CeoAgent.Application.Errors;
using CeoAgent.Shared.Calendar;
using CeoAgent.Infrastructure.Implementation.AITools.GoogleCalendar;
using Google;
using Google.Apis.Calendar.v3;
using Google.Apis.Calendar.v3.Data;

namespace CeoAgent.Infrastructure.Implementation.AITools.GoogleCalendar.Integration;

/// <summary>
/// Implements calendar availability and reservation operations against Google Calendar.
/// </summary>
public sealed class GoogleCalendarIntegration(IGoogleCalendarServiceFactory<CalendarService> googleCalendarServiceFactory)
    : IGoogleCalendarIntegration
{
    private const int MaxEventListPages = 20;
    private const string IdempotencyPropertyName = "ceoagent_idempotency_key";
    private const string CompanyPropertyName = "ceoagent_organization_id";
    private const string ConversationPropertyName = "ceoagent_conversation_id";
    private const string CustomerExternalIdPropertyName = "ceoagent_customer_external_id";
    private const string CustomerPhonePropertyName = "ceoagent_customer_phone";
    private const string CustomerNamePropertyName = "ceoagent_customer_name";
    private const string ReservationIdPropertyName = "ceoagent_reservation_id";
    private const string PaymentPendingDescriptionMarker = "[PAGO_PENDIENTE]";

    /// <summary>
    /// Checks whether the requested interval is free and returns configured nearby alternatives when it is busy.
    /// </summary>
    public async Task<CalendarAvailabilityResult> CheckAvailabilityAsync(
        CalendarAvailabilityRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        try
        {
            var service = await googleCalendarServiceFactory.CreateAsync(request.CredentialReference, cancellationToken);
            var duration = request.End - request.Start;
            var queryStart = request.SearchWindowStart.AddMinutes(-request.BufferMinutes);
            var queryEnd = request.SearchWindowEnd.Add(duration).AddMinutes(request.BufferMinutes);
            var busyRanges = await GetBusyRangesAsync(service, request.CalendarId, queryStart, queryEnd, cancellationToken);

            if (busyRanges is null)
            {
                return new CalendarAvailabilityResult(
                    Available: false,
                    AlternativeStarts: [],
                    UnavailabilityReason: "slot_unavailable");
            }

            var primaryAvailable = request.RequestedSlotEligible
                && IsAvailable(busyRanges, request.Start, request.End, request.BufferMinutes);

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
                    if (alternatives.Count == GoogleCalendarSchedulingPolicy.MaxAlternativeStarts)
                    {
                        break;
                    }
                }
            }

            return new CalendarAvailabilityResult(
                Available: false,
                AlternativeStarts: alternatives,
                UnavailabilityReason: "slot_unavailable");
        }
        catch (GoogleApiException exception)
        {
            return new CalendarAvailabilityResult(false, [], MapGoogleFailureReason(exception));
        }
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

            throw new IntegrationException(
                "google_calendar",
                MapGoogleFailureReason(exception),
                "Google Calendar reservation creation failed.",
                exception);
        }
        catch (GoogleApiException exception)
        {
            throw new IntegrationException(
                "google_calendar",
                MapGoogleFailureReason(exception),
                "Google Calendar reservation creation failed.",
                exception);
        }

        return new CalendarReservationResult(created.Id, created.HtmlLink);
    }

    public async Task<CalendarReservationSearchResult> FindReservationsAsync(
        CalendarReservationSearchRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        try
        {
            var service = await googleCalendarServiceFactory.CreateAsync(request.CredentialReference, cancellationToken);
            var eventsRequest = service.Events.List(request.CalendarId);
            eventsRequest.PrivateExtendedProperty = $"{CustomerPhonePropertyName}={request.CustomerExternalId}";
            eventsRequest.TimeMinDateTimeOffset = request.TimeMin;
            eventsRequest.TimeMaxDateTimeOffset = request.TimeMax;
            eventsRequest.SingleEvents = true;
            eventsRequest.ShowDeleted = false;
            eventsRequest.MaxResults = 50;
            eventsRequest.OrderBy = EventsResource.ListRequest.OrderByEnum.StartTime;

            var events = await ListEventsAsync(eventsRequest, cancellationToken);
            if (events.Count == 0)
            {
                var legacyEventsRequest = service.Events.List(request.CalendarId);
                legacyEventsRequest.PrivateExtendedProperty = $"{CustomerExternalIdPropertyName}={request.CustomerExternalId}";
                legacyEventsRequest.TimeMinDateTimeOffset = request.TimeMin;
                legacyEventsRequest.TimeMaxDateTimeOffset = request.TimeMax;
                legacyEventsRequest.SingleEvents = true;
                legacyEventsRequest.ShowDeleted = false;
                legacyEventsRequest.MaxResults = 50;
                legacyEventsRequest.OrderBy = EventsResource.ListRequest.OrderByEnum.StartTime;
                events = await ListEventsAsync(legacyEventsRequest, cancellationToken);
            }

            var reservations = events
                .Where(item => IsOwnedBy(item, request.OrganizationId, request.CustomerExternalId))
                .Select(ToReservationInfo)
                .Where(item => item is not null)
                .Cast<CalendarReservationInfo>()
                .ToArray();

            return new CalendarReservationSearchResult(reservations);
        }
        catch (GoogleApiException exception)
        {
            return CalendarReservationSearchResult.Failed(MapGoogleFailureReason(exception));
        }
    }

    public async Task<CalendarReservationMutationResult> UpdateReservationAsync(
        CalendarReservationUpdateRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        try
        {
            var service = await googleCalendarServiceFactory.CreateAsync(request.CredentialReference, cancellationToken);
            var existing = await ResolveOwnedReservationAsync(
                service,
                request.CalendarId,
                request.ReservationId,
                request.OrganizationId,
                request.CustomerExternalId,
                cancellationToken);
            if (existing is null)
            {
                return CalendarReservationMutationResult.NotOwned(request.ReservationId);
            }

            var slotAvailable = await IsUpdateSlotAvailableAsync(
                service,
                request.CalendarId,
                existing.Id,
                request.NewStart,
                request.NewEnd,
                request.BufferMinutes,
                cancellationToken);
            if (!slotAvailable)
            {
                return new CalendarReservationMutationResult(false, null, "slot_unavailable");
            }

            existing.Start = new EventDateTime
            {
                DateTimeDateTimeOffset = request.NewStart,
            };
            existing.End = new EventDateTime
            {
                DateTimeDateTimeOffset = request.NewEnd,
            };

            if (!string.IsNullOrWhiteSpace(request.Summary))
            {
                existing.Summary = BuildPendingPaymentSummary(request.Summary);
            }

            if (!string.IsNullOrWhiteSpace(request.CustomerName))
            {
                existing.Description = BuildDescription(request.CustomerName, request.CustomerExternalId);
                AddOrUpdateCustomerMetadata(existing, request.CustomerName, request.CustomerExternalId);
            }

            var updated = await service.Events.Update(existing, request.CalendarId, existing.Id)
                .ExecuteAsync(cancellationToken);
            var reservation = ToReservationInfo(updated);
            return reservation is null
                ? CalendarReservationMutationResult.NotOwned(request.ReservationId)
                : CalendarReservationMutationResult.Updated(reservation);
        }
        catch (GoogleApiException exception) when (exception.HttpStatusCode == HttpStatusCode.NotFound)
        {
            return CalendarReservationMutationResult.NotOwned(request.ReservationId);
        }
        catch (GoogleApiException exception)
        {
            return CalendarReservationMutationResult.Failed(MapGoogleFailureReason(exception));
        }
    }

    public async Task<CalendarReservationCancellationResult> CancelReservationAsync(
        CalendarReservationCancellationRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        try
        {
            var service = await googleCalendarServiceFactory.CreateAsync(request.CredentialReference, cancellationToken);
            var existing = await ResolveOwnedReservationAsync(
                service,
                request.CalendarId,
                request.ReservationId,
                request.OrganizationId,
                request.CustomerExternalId,
                cancellationToken);
            if (existing is null)
            {
                return CalendarReservationCancellationResult.NotOwned(request.ReservationId);
            }

            await service.Events.Delete(request.CalendarId, existing.Id)
                .ExecuteAsync(cancellationToken);
            return CalendarReservationCancellationResult.Cancelled(
                ReservationId(existing),
                existing.Id);
        }
        catch (GoogleApiException exception) when (exception.HttpStatusCode == HttpStatusCode.NotFound)
        {
            return CalendarReservationCancellationResult.NotOwned(request.ReservationId);
        }
        catch (GoogleApiException exception)
        {
            return CalendarReservationCancellationResult.Failed(
                request.ReservationId,
                MapGoogleFailureReason(exception));
        }
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
            Summary = BuildPendingPaymentSummary(request.Summary),
            Description = BuildDescription(
                request.CustomerName ?? ParseCustomerName(request.Description),
                request.CustomerPhoneNumber ?? request.CustomerExternalId,
                request.Description),
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
                Private__ = BuildPrivateProperties(request),
            },
        };
    }

    private static async Task<Event?> ResolveOwnedReservationAsync(
        CalendarService service,
        string calendarId,
        string reservationId,
        string organizationId,
        string customerExternalId,
        CancellationToken cancellationToken)
    {
        var byEventId = await TryGetEventAsync(service, calendarId, reservationId, cancellationToken);
        if (byEventId is not null && IsOwnedBy(byEventId, organizationId, customerExternalId))
        {
            return byEventId;
        }

        var byReservationId = service.Events.List(calendarId);
        byReservationId.PrivateExtendedProperty = $"{ReservationIdPropertyName}={reservationId}";
        byReservationId.SingleEvents = true;
        byReservationId.ShowDeleted = false;
        byReservationId.MaxResults = 10;

        var events = await ListEventsAsync(byReservationId, cancellationToken);
        return events.FirstOrDefault(item => IsOwnedBy(item, organizationId, customerExternalId));
    }

    private static async Task<bool> IsUpdateSlotAvailableAsync(
        CalendarService service,
        string calendarId,
        string eventId,
        DateTimeOffset start,
        DateTimeOffset end,
        int bufferMinutes,
        CancellationToken cancellationToken)
    {
        var eventsRequest = service.Events.List(calendarId);
        eventsRequest.TimeMinDateTimeOffset = start.AddMinutes(-bufferMinutes);
        eventsRequest.TimeMaxDateTimeOffset = end.AddMinutes(bufferMinutes);
        eventsRequest.SingleEvents = true;
        eventsRequest.ShowDeleted = false;
        eventsRequest.MaxResults = 50;

        var events = await ListEventsAsync(eventsRequest, cancellationToken);
        return events.All(item =>
            string.Equals(item.Id, eventId, StringComparison.Ordinal)
            || !Overlaps(item, start, end, bufferMinutes));
    }

    private static async Task<IReadOnlyList<Event>> ListEventsAsync(
        EventsResource.ListRequest request,
        CancellationToken cancellationToken)
    {
        var events = new List<Event>();
        for (var page = 0; page < MaxEventListPages; page++)
        {
            var response = await request.ExecuteAsync(cancellationToken);
            if (response.Items is not null)
            {
                events.AddRange(response.Items);
            }

            if (string.IsNullOrWhiteSpace(response.NextPageToken))
            {
                break;
            }

            request.PageToken = response.NextPageToken;
        }

        return events;
    }

    private static bool Overlaps(Event calendarEvent, DateTimeOffset start, DateTimeOffset end, int bufferMinutes)
    {
        if (calendarEvent.Start?.DateTimeDateTimeOffset is null
            || calendarEvent.End?.DateTimeDateTimeOffset is null)
        {
            return false;
        }

        var bufferedStart = start.AddMinutes(-bufferMinutes);
        var bufferedEnd = end.AddMinutes(bufferMinutes);
        return calendarEvent.Start.DateTimeDateTimeOffset.Value < bufferedEnd
            && calendarEvent.End.DateTimeDateTimeOffset.Value > bufferedStart;
    }

    private static async Task<Event?> TryGetEventAsync(
        CalendarService service,
        string calendarId,
        string eventId,
        CancellationToken cancellationToken)
    {
        try
        {
            return await service.Events.Get(calendarId, eventId).ExecuteAsync(cancellationToken);
        }
        catch (GoogleApiException exception) when (exception.HttpStatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    private static string MapGoogleFailureReason(GoogleApiException exception)
    {
        return exception.HttpStatusCode switch
        {
            HttpStatusCode.TooManyRequests => "rate_limited",
            HttpStatusCode.ServiceUnavailable or HttpStatusCode.BadGateway or HttpStatusCode.GatewayTimeout
                or HttpStatusCode.InternalServerError => "upstream_error",
            HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => "calendar_access_denied",
            _ => "upstream_error",
        };
    }

    private static bool IsOwnedBy(Event calendarEvent, string organizationId, string customerExternalId)
    {
        var privateProperties = calendarEvent.ExtendedProperties?.Private__;
        return privateProperties is not null
            && privateProperties.TryGetValue(CompanyPropertyName, out var eventOrganizationId)
            && string.Equals(eventOrganizationId, organizationId, StringComparison.Ordinal)
            && IsCurrentCustomer(privateProperties, customerExternalId);
    }

    private static CalendarReservationInfo? ToReservationInfo(Event calendarEvent)
    {
        if (string.IsNullOrWhiteSpace(calendarEvent.Id)
            || calendarEvent.Start?.DateTimeDateTimeOffset is null
            || calendarEvent.End?.DateTimeDateTimeOffset is null)
        {
            return null;
        }

        return new CalendarReservationInfo(
            ReservationId(calendarEvent),
            calendarEvent.Id,
            calendarEvent.Start.DateTimeDateTimeOffset.Value,
            calendarEvent.End.DateTimeDateTimeOffset.Value,
            calendarEvent.Summary,
            CustomerName(calendarEvent),
            calendarEvent.HtmlLink,
            CustomerPhoneNumber(calendarEvent));
    }

    private static Dictionary<string, string> BuildPrivateProperties(CalendarReservationRequest request)
    {
        var properties = new Dictionary<string, string>
        {
            [IdempotencyPropertyName] = request.IdempotencyKey,
            [CompanyPropertyName] = request.OrganizationId ?? string.Empty,
            [ConversationPropertyName] = request.ConversationId ?? string.Empty,
            [CustomerExternalIdPropertyName] = request.CustomerExternalId ?? string.Empty,
            [ReservationIdPropertyName] = request.ReservationId ?? request.IdempotencyKey,
        };

        var customerPhoneNumber = request.CustomerPhoneNumber ?? request.CustomerExternalId;
        if (!string.IsNullOrWhiteSpace(customerPhoneNumber))
        {
            properties[CustomerPhonePropertyName] = customerPhoneNumber.Trim();
        }

        var customerName = request.CustomerName ?? ParseCustomerName(request.Description);
        if (!string.IsNullOrWhiteSpace(customerName))
        {
            properties[CustomerNamePropertyName] = customerName.Trim();
        }

        return properties;
    }

    private static void AddOrUpdateCustomerMetadata(Event calendarEvent, string? customerName, string? customerPhoneNumber)
    {
        calendarEvent.ExtendedProperties ??= new Event.ExtendedPropertiesData();
        calendarEvent.ExtendedProperties.Private__ ??= new Dictionary<string, string>();

        if (!string.IsNullOrWhiteSpace(customerPhoneNumber))
        {
            calendarEvent.ExtendedProperties.Private__[CustomerPhonePropertyName] = customerPhoneNumber.Trim();
        }

        if (!string.IsNullOrWhiteSpace(customerName))
        {
            calendarEvent.ExtendedProperties.Private__[CustomerNamePropertyName] = customerName.Trim();
        }
    }

    private static bool IsCurrentCustomer(IDictionary<string, string> privateProperties, string customerExternalId)
    {
        return privateProperties.TryGetValue(CustomerPhonePropertyName, out var eventCustomerPhone)
                && string.Equals(eventCustomerPhone, customerExternalId, StringComparison.Ordinal)
            || privateProperties.TryGetValue(CustomerExternalIdPropertyName, out var eventCustomerExternalId)
                && string.Equals(eventCustomerExternalId, customerExternalId, StringComparison.Ordinal);
    }

    private static string? CustomerName(Event calendarEvent)
    {
        return calendarEvent.ExtendedProperties?.Private__ is { } privateProperties
            && privateProperties.TryGetValue(CustomerNamePropertyName, out var customerName)
            && !string.IsNullOrWhiteSpace(customerName)
            ? customerName
            : ParseCustomerName(calendarEvent.Description);
    }

    private static string? CustomerPhoneNumber(Event calendarEvent)
    {
        var privateProperties = calendarEvent.ExtendedProperties?.Private__;
        if (privateProperties is null)
        {
            return null;
        }

        if (privateProperties.TryGetValue(CustomerPhonePropertyName, out var customerPhoneNumber)
            && !string.IsNullOrWhiteSpace(customerPhoneNumber))
        {
            return customerPhoneNumber;
        }

        return privateProperties.TryGetValue(CustomerExternalIdPropertyName, out var customerExternalId)
            && !string.IsNullOrWhiteSpace(customerExternalId)
            ? customerExternalId
            : null;
    }

    private static string? BuildDescription(string? customerName, string? customerPhoneNumber, string? fallbackDescription = null)
    {
        var lines = new List<string>(capacity: 4)
        {
            PaymentPendingDescriptionMarker,
        };
        var hasCustomerDetails = false;

        if (!string.IsNullOrWhiteSpace(customerName))
        {
            lines.Add($"Customer: {customerName.Trim()}");
            hasCustomerDetails = true;
        }

        if (!string.IsNullOrWhiteSpace(customerPhoneNumber))
        {
            lines.Add($"Phone: {customerPhoneNumber.Trim()}");
            hasCustomerDetails = true;
        }

        if (!hasCustomerDetails && !string.IsNullOrWhiteSpace(fallbackDescription))
        {
            lines.Add(fallbackDescription.Trim());
        }

        return string.Join('\n', lines);
    }

    private static string BuildPendingPaymentSummary(string summary)
    {
        var trimmed = summary.Trim();
        return trimmed.StartsWith(PaymentPendingDescriptionMarker, StringComparison.Ordinal)
            ? trimmed
            : $"{PaymentPendingDescriptionMarker} {trimmed}";
    }

    private static string ReservationId(Event calendarEvent)
    {
        return calendarEvent.ExtendedProperties?.Private__ is { } privateProperties
            && privateProperties.TryGetValue(ReservationIdPropertyName, out var reservationId)
            && !string.IsNullOrWhiteSpace(reservationId)
            ? reservationId
            : calendarEvent.Id;
    }

    private static string? ParseCustomerName(string? description)
    {
        const string prefix = "Customer:";
        if (string.IsNullOrWhiteSpace(description)
            || !description.TrimStart().StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var value = description.TrimStart()[prefix.Length..].Trim();
        return string.IsNullOrWhiteSpace(value) ? null : value;
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
