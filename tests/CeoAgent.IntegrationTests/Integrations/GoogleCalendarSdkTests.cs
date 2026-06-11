using System.Globalization;
using System.Net;
using System.Reflection;
using System.IO.Compression;
using System.Text.Json;
using CeoAgent.Application.Abstractions.AITools.GoogleCalendar;
using CeoAgent.Infrastructure;
using CeoAgent.Infrastructure.Implementation.AITools.GoogleCalendar.Integration;
using CeoAgent.Shared.Calendar;
using Google.Apis.Calendar.v3;
using Google.Apis.Http;
using Google.Apis.Services;
using Shouldly;

namespace CeoAgent.IntegrationTests.Integrations;

public sealed class GoogleCalendarSdkTests
{
    [Test]
    public void InfrastructureProject_ReferencesOfficialGoogleCalendarSdk()
    {
        typeof(InfrastructureAssembly).Assembly
            .GetReferencedAssemblies()
            .Any(assembly => assembly.Name == "Google.Apis.Calendar.v3")
            .ShouldBeTrue();
    }

    [Test]
    public void GoogleCalendarIntegrationContract_IsProviderNeutral()
    {
        typeof(IGoogleCalendarIntegration).GetMethod(nameof(IGoogleCalendarIntegration.CheckAvailabilityAsync)).ShouldNotBeNull();
        typeof(IGoogleCalendarIntegration).GetMethod(nameof(IGoogleCalendarIntegration.CreateReservationAsync)).ShouldNotBeNull();
        typeof(IGoogleCalendarIntegration).GetMethod(nameof(IGoogleCalendarIntegration.FindReservationsAsync)).ShouldNotBeNull();
        typeof(IGoogleCalendarIntegration).GetMethod(nameof(IGoogleCalendarIntegration.UpdateReservationAsync)).ShouldNotBeNull();
        typeof(IGoogleCalendarIntegration).GetMethod(nameof(IGoogleCalendarIntegration.CancelReservationAsync)).ShouldNotBeNull();

        typeof(IGoogleCalendarIntegration)
            .GetMethods()
            .SelectMany(method => method.GetParameters().Select(parameter => parameter.ParameterType).Append(method.ReturnType))
            .SelectMany(GetExposedTypes)
            .Any(type => type.Namespace?.StartsWith("Google.Apis", StringComparison.Ordinal) == true)
            .ShouldBeFalse();
    }

    private static IEnumerable<Type> GetExposedTypes(Type type)
    {
        yield return type;

        if (!type.IsGenericType)
        {
            yield break;
        }

        foreach (var genericArgument in type.GetGenericArguments())
        {
            foreach (var exposedType in GetExposedTypes(genericArgument))
            {
                yield return exposedType;
            }
        }
    }

    [Test]
    public void Adapter_UsesCalendarServiceFactory()
    {
        typeof(GoogleCalendarIntegration)
            .GetConstructors(BindingFlags.Public | BindingFlags.Instance)
            .SelectMany(constructor => constructor.GetParameters())
            .Any(parameter => parameter.ParameterType == typeof(IGoogleCalendarServiceFactory<CalendarService>))
            .ShouldBeTrue();
    }

    [Test]
    public async Task CheckAvailabilityAsync_QueriesFreeBusyOnceForRequestedAndAlternativeRange()
    {
        var handler = new RecordingGoogleCalendarHandler
        {
            FreeBusyResponseJson = """
                {
                  "calendars": {
                    "primary": {
                      "busy": [
                        {
                          "start": "2026-05-28T16:00:00-05:00",
                          "end": "2026-05-28T17:00:00-05:00"
                        },
                        {
                          "start": "2026-05-28T16:30:00-05:00",
                          "end": "2026-05-28T17:30:00-05:00"
                        }
                      ]
                    }
                  }
                }
                """,
        };
        var integration = new GoogleCalendarIntegration(new RecordingGoogleCalendarServiceFactory(handler));

        var result = await integration.CheckAvailabilityAsync(
            new CalendarAvailabilityRequest(
                CredentialReference: "default",
                CalendarId: "primary",
                Start: new DateTimeOffset(2026, 5, 28, 16, 0, 0, TimeSpan.FromHours(-5)),
                End: new DateTimeOffset(2026, 5, 28, 17, 0, 0, TimeSpan.FromHours(-5)),
                SearchWindowStart: new DateTimeOffset(2026, 5, 28, 13, 0, 0, TimeSpan.FromHours(-5)),
                SearchWindowEnd: new DateTimeOffset(2026, 5, 28, 19, 0, 0, TimeSpan.FromHours(-5)),
                PartySize: 2,
                AlternativeSearchStarts:
                [
                    new DateTimeOffset(2026, 5, 28, 15, 30, 0, TimeSpan.FromHours(-5)),
                    new DateTimeOffset(2026, 5, 28, 16, 30, 0, TimeSpan.FromHours(-5)),
                    new DateTimeOffset(2026, 5, 28, 17, 30, 0, TimeSpan.FromHours(-5)),
                    new DateTimeOffset(2026, 5, 28, 18, 0, 0, TimeSpan.FromHours(-5)),
                ]),
            CancellationToken.None);

        handler.Requests.Count(request => request.RequestUri?.AbsolutePath == "/calendar/v3/freeBusy").ShouldBe(1);
        var freeBusyRequest = handler.JsonBodies.Single();
        DateTimeOffset.Parse(freeBusyRequest.RootElement.GetProperty("timeMin").GetString()!, CultureInfo.InvariantCulture)
            .ShouldBe(new DateTimeOffset(2026, 5, 28, 13, 0, 0, TimeSpan.FromHours(-5)));
        DateTimeOffset.Parse(freeBusyRequest.RootElement.GetProperty("timeMax").GetString()!, CultureInfo.InvariantCulture)
            .ShouldBe(new DateTimeOffset(2026, 5, 28, 20, 0, 0, TimeSpan.FromHours(-5)));
        freeBusyRequest.RootElement.GetProperty("items")[0].GetProperty("id").GetString().ShouldBe("primary");
        result.Available.ShouldBeFalse();
        result.AlternativeStarts.ShouldBe(
        [
            new DateTimeOffset(2026, 5, 28, 17, 30, 0, TimeSpan.FromHours(-5)),
            new DateTimeOffset(2026, 5, 28, 18, 0, 0, TimeSpan.FromHours(-5)),
        ]);
    }

    [Test]
    public async Task CreateReservationAsync_WhenIdempotencyKeyMatchesExistingEvent_ReturnsExistingEvent()
    {
        var handler = new RecordingGoogleCalendarHandler
        {
            EventsResponseJson = """
                {
                  "items": [
                    {
                      "id": "event-existing",
                      "htmlLink": "https://calendar.google.com/event?eid=event-existing"
                    }
                  ]
                }
                """,
        };
        var integration = new GoogleCalendarIntegration(new RecordingGoogleCalendarServiceFactory(handler));

        var result = await integration.CreateReservationAsync(
            new CalendarReservationRequest(
                CredentialReference: "default",
                CalendarId: "primary",
                Start: new DateTimeOffset(2026, 5, 28, 16, 0, 0, TimeSpan.FromHours(-5)),
                End: new DateTimeOffset(2026, 5, 28, 17, 0, 0, TimeSpan.FromHours(-5)),
                Summary: "Reservation for 2",
                IdempotencyKey: "reservation-key",
                Description: null,
                CustomerEmail: null),
            CancellationToken.None);

        result.EventId.ShouldBe("event-existing");
        result.EventUrl.ShouldBe("https://calendar.google.com/event?eid=event-existing");

        var eventsQuery = handler.Requests.Single(request => request.Method == HttpMethod.Get);
        eventsQuery.RequestUri.ShouldNotBeNull();
        eventsQuery.RequestUri.AbsolutePath.ShouldBe("/calendar/v3/calendars/primary/events");
        eventsQuery.RequestUri.Query.ShouldContain("privateExtendedProperty=ceoagent_idempotency_key%3Dreservation-key");
        eventsQuery.RequestUri.Query.ShouldContain("singleEvents=true");
        eventsQuery.RequestUri.Query.ShouldContain("maxResults=1");
        handler.Requests.Count(request => request.Method == HttpMethod.Post
            && request.RequestUri?.AbsolutePath.EndsWith("/events", StringComparison.Ordinal) == true).ShouldBe(0);
    }

    [Test]
    public async Task CreateReservationAsync_WhenNoExistingEvent_CreatesEventWithIdempotencyKey()
    {
        var handler = new RecordingGoogleCalendarHandler
        {
            EventsResponseJson = """{"items":[]}""",
            CreatedEventJson = """
                {
                  "id": "event-created",
                  "htmlLink": "https://calendar.google.com/event?eid=event-created"
                }
                """,
        };
        var integration = new GoogleCalendarIntegration(new RecordingGoogleCalendarServiceFactory(handler));

        var result = await integration.CreateReservationAsync(
            new CalendarReservationRequest(
                CredentialReference: "default",
                CalendarId: "primary",
                Start: new DateTimeOffset(2026, 5, 28, 16, 0, 0, TimeSpan.FromHours(-5)),
                End: new DateTimeOffset(2026, 5, 28, 17, 0, 0, TimeSpan.FromHours(-5)),
                Summary: "Reservation for 2",
                IdempotencyKey: "reservation-key",
                Description: "Window table",
                CustomerEmail: "customer@example.com"),
            CancellationToken.None);

        result.EventId.ShouldBe("event-created");

        var created = handler.JsonBodies.Last();
        created.RootElement.GetProperty("id").GetString().ShouldStartWith("ceoagent");
        created.RootElement.GetProperty("summary").GetString().ShouldBe("Reservation for 2");
        created.RootElement.GetProperty("description").GetString().ShouldBe("Window table");
        DateTimeOffset.Parse(created.RootElement.GetProperty("start").GetProperty("dateTime").GetString()!, CultureInfo.InvariantCulture)
            .ShouldBe(new DateTimeOffset(2026, 5, 28, 16, 0, 0, TimeSpan.FromHours(-5)));
        DateTimeOffset.Parse(created.RootElement.GetProperty("end").GetProperty("dateTime").GetString()!, CultureInfo.InvariantCulture)
            .ShouldBe(new DateTimeOffset(2026, 5, 28, 17, 0, 0, TimeSpan.FromHours(-5)));
        created.RootElement.GetProperty("attendees")[0].GetProperty("email").GetString().ShouldBe("customer@example.com");
        created.RootElement
            .GetProperty("extendedProperties")
            .GetProperty("private")
            .GetProperty("ceoagent_idempotency_key")
            .GetString()
            .ShouldBe("reservation-key");
    }

    [Test]
    public async Task CreateReservationAsync_WhenNoExistingEvent_CreatesEventWithCustomerOwnershipMetadata()
    {
        var handler = new RecordingGoogleCalendarHandler
        {
            EventsResponseJson = """{"items":[]}""",
        };
        var integration = new GoogleCalendarIntegration(new RecordingGoogleCalendarServiceFactory(handler));

        await integration.CreateReservationAsync(
            new CalendarReservationRequest(
                CredentialReference: "default",
                CalendarId: "primary",
                Start: new DateTimeOffset(2026, 5, 28, 16, 0, 0, TimeSpan.FromHours(-5)),
                End: new DateTimeOffset(2026, 5, 28, 17, 0, 0, TimeSpan.FromHours(-5)),
                Summary: "Reservation for 2",
                IdempotencyKey: "reservation-key",
                Description: "Customer: Ada Lovelace",
                CustomerEmail: null,
                OrganizationId: "018f4f70-8b5f-7b4c-9d1a-0f6c1d7a2b30",
                ConversationId: "018f4f70-8b5f-7b4c-9d1a-0f6c1d7a2b34",
                CustomerExternalId: "15551234567",
                ReservationId: "reservation-key"),
            CancellationToken.None);

        var privateProperties = handler.JsonBodies.Last()
            .RootElement
            .GetProperty("extendedProperties")
            .GetProperty("private");
        privateProperties.GetProperty("ceoagent_organization_id").GetString().ShouldBe("018f4f70-8b5f-7b4c-9d1a-0f6c1d7a2b30");
        privateProperties.GetProperty("ceoagent_conversation_id").GetString().ShouldBe("018f4f70-8b5f-7b4c-9d1a-0f6c1d7a2b34");
        privateProperties.GetProperty("ceoagent_customer_external_id").GetString().ShouldBe("15551234567");
        privateProperties.GetProperty("ceoagent_reservation_id").GetString().ShouldBe("reservation-key");
    }

    [Test]
    public async Task FindReservationsAsync_QueriesByCustomerPrivateExtendedPropertyAndReturnsSanitizedMatches()
    {
        var handler = new RecordingGoogleCalendarHandler
        {
            EventsResponseJson = """
                {
                  "items": [
                    {
                      "id": "event-123",
                      "htmlLink": "https://calendar.google.com/event?eid=event-123",
                      "summary": "Reservation for 2",
                      "description": "Customer: Ada Lovelace",
                      "start": { "dateTime": "2026-05-28T16:00:00-05:00" },
                      "end": { "dateTime": "2026-05-28T17:00:00-05:00" },
                      "extendedProperties": {
                        "private": {
                          "ceoagent_organization_id": "company-1",
                          "ceoagent_customer_external_id": "15551234567",
                          "ceoagent_reservation_id": "reservation-key"
                        }
                      }
                    }
                  ]
                }
                """,
        };
        var integration = new GoogleCalendarIntegration(new RecordingGoogleCalendarServiceFactory(handler));

        var result = await integration.FindReservationsAsync(
            new CalendarReservationSearchRequest(
                "default",
                "primary",
                "company-1",
                "15551234567",
                new DateTimeOffset(2026, 5, 28, 0, 0, 0, TimeSpan.FromHours(-5)),
                new DateTimeOffset(2026, 5, 29, 0, 0, 0, TimeSpan.FromHours(-5)),
                IncludePast: false),
            CancellationToken.None);

        var eventsQuery = handler.Requests.Single(request => request.Method == HttpMethod.Get);
        eventsQuery.RequestUri!.Query.ShouldContain("privateExtendedProperty=ceoagent_customer_external_id%3D15551234567");
        result.Reservations.Single().ReservationId.ShouldBe("reservation-key");
        result.Reservations.Single().CustomerName.ShouldBe("Ada Lovelace");
    }

    [Test]
    public async Task FindReservationsAsync_WhenEventsListIsPaginated_ReturnsOwnedMatchesFromAllPages()
    {
        var handler = new RecordingGoogleCalendarHandler
        {
            EventsResponseJsonSequence =
            [
                """
                {
                  "nextPageToken": "page-2",
                  "items": [
                    {
                      "id": "event-1",
                      "summary": "Reservation page 1",
                      "start": { "dateTime": "2026-05-28T16:00:00-05:00" },
                      "end": { "dateTime": "2026-05-28T17:00:00-05:00" },
                      "extendedProperties": {
                        "private": {
                          "ceoagent_organization_id": "company-1",
                          "ceoagent_customer_external_id": "15551234567",
                          "ceoagent_reservation_id": "reservation-1"
                        }
                      }
                    }
                  ]
                }
                """,
                """
                {
                  "items": [
                    {
                      "id": "event-2",
                      "summary": "Reservation page 2",
                      "start": { "dateTime": "2026-05-28T18:00:00-05:00" },
                      "end": { "dateTime": "2026-05-28T19:00:00-05:00" },
                      "extendedProperties": {
                        "private": {
                          "ceoagent_organization_id": "company-1",
                          "ceoagent_customer_external_id": "15551234567",
                          "ceoagent_reservation_id": "reservation-2"
                        }
                      }
                    },
                    {
                      "id": "event-other",
                      "summary": "Other customer",
                      "start": { "dateTime": "2026-05-28T19:00:00-05:00" },
                      "end": { "dateTime": "2026-05-28T20:00:00-05:00" },
                      "extendedProperties": {
                        "private": {
                          "ceoagent_organization_id": "company-1",
                          "ceoagent_customer_external_id": "other-customer",
                          "ceoagent_reservation_id": "reservation-other"
                        }
                      }
                    }
                  ]
                }
                """,
            ],
        };
        var integration = new GoogleCalendarIntegration(new RecordingGoogleCalendarServiceFactory(handler));

        var result = await integration.FindReservationsAsync(
            new CalendarReservationSearchRequest(
                "default",
                "primary",
                "company-1",
                "15551234567",
                new DateTimeOffset(2026, 5, 28, 0, 0, 0, TimeSpan.FromHours(-5)),
                new DateTimeOffset(2026, 5, 29, 0, 0, 0, TimeSpan.FromHours(-5)),
                IncludePast: false),
            CancellationToken.None);

        var eventListRequests = handler.Requests
            .Where(request => request.Method == HttpMethod.Get
                && request.RequestUri?.AbsolutePath.EndsWith("/events", StringComparison.Ordinal) == true)
            .ToArray();
        eventListRequests.Length.ShouldBe(2);
        eventListRequests[1].RequestUri!.Query.ShouldContain("pageToken=page-2");
        result.Reservations.Select(item => item.ReservationId).ShouldBe(["reservation-1", "reservation-2"]);
    }

    [Test]
    public async Task UpdateReservationAsync_WhenMetadataDoesNotMatch_DoesNotUpdateEvent()
    {
        var handler = new RecordingGoogleCalendarHandler
        {
            EventResponseJson = """
                {
                  "id": "event-123",
                  "start": { "dateTime": "2026-05-28T16:00:00-05:00" },
                  "end": { "dateTime": "2026-05-28T17:00:00-05:00" },
                  "extendedProperties": {
                    "private": {
                      "ceoagent_organization_id": "company-1",
                      "ceoagent_customer_external_id": "other-customer"
                    }
                  }
                }
                """,
            EventsResponseJson = """{"items":[]}""",
        };
        var integration = new GoogleCalendarIntegration(new RecordingGoogleCalendarServiceFactory(handler));

        var result = await integration.UpdateReservationAsync(
            new CalendarReservationUpdateRequest(
                "default",
                "primary",
                "company-1",
                "15551234567",
                "event-123",
                new DateTimeOffset(2026, 5, 28, 20, 0, 0, TimeSpan.FromHours(-5)),
                new DateTimeOffset(2026, 5, 28, 21, 0, 0, TimeSpan.FromHours(-5)),
                Summary: null,
                CustomerName: null),
            CancellationToken.None);

        result.Succeeded.ShouldBeFalse();
        handler.Requests.Count(request => request.Method == HttpMethod.Put).ShouldBe(0);
    }

    [Test]
    public async Task UpdateReservationAsync_WhenAvailabilityListConflictIsOnSecondPage_ReturnsSlotUnavailableAndDoesNotUpdate()
    {
        var handler = new RecordingGoogleCalendarHandler
        {
            EventsResponseJsonSequence =
            [
                """
                {
                  "nextPageToken": "busy-page-2",
                  "items": [
                    {
                      "id": "event-123",
                      "start": { "dateTime": "2026-05-28T16:00:00-05:00" },
                      "end": { "dateTime": "2026-05-28T17:00:00-05:00" }
                    }
                  ]
                }
                """,
                """
                {
                  "items": [
                    {
                      "id": "event-conflict",
                      "start": { "dateTime": "2026-05-28T20:30:00-05:00" },
                      "end": { "dateTime": "2026-05-28T21:30:00-05:00" }
                    }
                  ]
                }
                """,
            ],
        };
        var integration = new GoogleCalendarIntegration(new RecordingGoogleCalendarServiceFactory(handler));

        var result = await integration.UpdateReservationAsync(
            new CalendarReservationUpdateRequest(
                "default",
                "primary",
                "company-1",
                "15551234567",
                "event-123",
                new DateTimeOffset(2026, 5, 28, 20, 0, 0, TimeSpan.FromHours(-5)),
                new DateTimeOffset(2026, 5, 28, 21, 0, 0, TimeSpan.FromHours(-5)),
                Summary: null,
                CustomerName: null),
            CancellationToken.None);

        result.Succeeded.ShouldBeFalse();
        result.FailureReason.ShouldBe("slot_unavailable");
        handler.Requests.Count(request => request.Method == HttpMethod.Put).ShouldBe(0);
        handler.Requests.Count(request => request.Method == HttpMethod.Get
            && request.RequestUri?.AbsolutePath.EndsWith("/events", StringComparison.Ordinal) == true).ShouldBe(2);
    }

    [Test]
    public async Task UpdateReservationAsync_WhenReservationIdLookupIsPaginated_FindsOwnedEventOnLaterPage()
    {
        var handler = new RecordingGoogleCalendarHandler
        {
            EventResponseStatusCode = HttpStatusCode.NotFound,
            EventResponseJson = """{"error":{"code":404,"message":"Not found."}}""",
            CreatedEventJson = """
                {
                  "id": "event-456",
                  "htmlLink": "https://calendar.google.com/event?eid=event-456",
                  "summary": "Reservation for 2",
                  "start": { "dateTime": "2026-05-28T20:00:00-05:00" },
                  "end": { "dateTime": "2026-05-28T21:00:00-05:00" },
                  "extendedProperties": {
                    "private": {
                      "ceoagent_organization_id": "company-1",
                      "ceoagent_customer_external_id": "15551234567",
                      "ceoagent_reservation_id": "reservation-key"
                    }
                  }
                }
                """,
            EventsResponseJsonSequence =
            [
                """
                {
                  "nextPageToken": "reservation-page-2",
                  "items": [
                    {
                      "id": "event-other",
                      "start": { "dateTime": "2026-05-28T16:00:00-05:00" },
                      "end": { "dateTime": "2026-05-28T17:00:00-05:00" },
                      "extendedProperties": {
                        "private": {
                          "ceoagent_organization_id": "company-1",
                          "ceoagent_customer_external_id": "other-customer",
                          "ceoagent_reservation_id": "reservation-key"
                        }
                      }
                    }
                  ]
                }
                """,
                """
                {
                  "items": [
                    {
                      "id": "event-456",
                      "htmlLink": "https://calendar.google.com/event?eid=event-456",
                      "summary": "Reservation for 2",
                      "start": { "dateTime": "2026-05-28T16:00:00-05:00" },
                      "end": { "dateTime": "2026-05-28T17:00:00-05:00" },
                      "extendedProperties": {
                        "private": {
                          "ceoagent_organization_id": "company-1",
                          "ceoagent_customer_external_id": "15551234567",
                          "ceoagent_reservation_id": "reservation-key"
                        }
                      }
                    }
                  ]
                }
                """,
                """{"items":[]}""",
            ],
        };
        var integration = new GoogleCalendarIntegration(new RecordingGoogleCalendarServiceFactory(handler));

        var result = await integration.UpdateReservationAsync(
            new CalendarReservationUpdateRequest(
                "default",
                "primary",
                "company-1",
                "15551234567",
                "reservation-key",
                new DateTimeOffset(2026, 5, 28, 20, 0, 0, TimeSpan.FromHours(-5)),
                new DateTimeOffset(2026, 5, 28, 21, 0, 0, TimeSpan.FromHours(-5)),
                Summary: null,
                CustomerName: null),
            CancellationToken.None);

        result.Succeeded.ShouldBeTrue();
        result.Reservation!.EventId.ShouldBe("event-456");
        handler.Requests.Count(request => request.Method == HttpMethod.Put).ShouldBe(1);
        handler.Requests.Count(request => request.Method == HttpMethod.Get
            && request.RequestUri?.Query.Contains("pageToken=reservation-page-2", StringComparison.Ordinal) == true).ShouldBe(1);
    }

    [Test]
    public async Task CreateReservationAsync_WhenDeterministicEventIdConflicts_ReturnsExistingReservation()
    {
        var handler = new RecordingGoogleCalendarHandler
        {
            EventsResponseJsonSequence =
            [
                """{"items":[]}""",
                """
                {
                  "items": [
                    {
                      "id": "event-existing",
                      "htmlLink": "https://calendar.google.com/event?eid=event-existing"
                    }
                  ]
                }
                """,
            ],
            CreatedEventStatusCode = HttpStatusCode.Conflict,
            CreatedEventJson = """{"error":{"code":409,"message":"The requested identifier already exists."}}""",
        };
        var integration = new GoogleCalendarIntegration(new RecordingGoogleCalendarServiceFactory(handler));

        var result = await integration.CreateReservationAsync(
            new CalendarReservationRequest(
                CredentialReference: "default",
                CalendarId: "primary",
                Start: new DateTimeOffset(2026, 5, 28, 16, 0, 0, TimeSpan.FromHours(-5)),
                End: new DateTimeOffset(2026, 5, 28, 17, 0, 0, TimeSpan.FromHours(-5)),
                Summary: "Reservation for 2",
                IdempotencyKey: "reservation-key",
                Description: null,
                CustomerEmail: null),
            CancellationToken.None);

        result.EventId.ShouldBe("event-existing");
        handler.Requests.Count(request => request.Method == HttpMethod.Get).ShouldBe(2);
        handler.Requests.Count(request => request.Method == HttpMethod.Post).ShouldBe(1);
    }

    private sealed class RecordingGoogleCalendarServiceFactory(RecordingGoogleCalendarHandler handler)
        : IGoogleCalendarServiceFactory<CalendarService>
    {
        public Task<CalendarService> CreateAsync(
            string credentialReference,
            CancellationToken cancellationToken)
        {
            var service = new CalendarService(new BaseClientService.Initializer
            {
                ApplicationName = "CEOAgent.Tests",
                HttpClientFactory = new RecordingGoogleHttpClientFactory(handler),
            });

            return Task.FromResult(service);
        }
    }

    private sealed class RecordingGoogleHttpClientFactory(RecordingGoogleCalendarHandler handler)
        : Google.Apis.Http.IHttpClientFactory
    {
        public ConfigurableHttpClient CreateHttpClient(CreateHttpClientArgs args)
        {
            return new ConfigurableHttpClient(new ConfigurableMessageHandler(handler));
        }
    }

    private sealed class RecordingGoogleCalendarHandler : HttpMessageHandler
    {
        private int eventsResponseIndex;

        public List<HttpRequestMessage> Requests { get; } = [];

        public List<JsonDocument> JsonBodies { get; } = [];

        public string FreeBusyResponseJson { get; init; } = """{"calendars":{"primary":{"busy":[]}}}""";

        public string EventsResponseJson { get; init; } = """{"items":[]}""";

        public string EventResponseJson { get; init; } =
            """
            {
              "id": "event-123",
              "htmlLink": "https://calendar.google.com/event?eid=event-123",
              "start": { "dateTime": "2026-05-28T16:00:00-05:00" },
              "end": { "dateTime": "2026-05-28T17:00:00-05:00" },
              "extendedProperties": {
                "private": {
                  "ceoagent_organization_id": "company-1",
                  "ceoagent_customer_external_id": "15551234567",
                  "ceoagent_reservation_id": "reservation-key"
                }
              }
            }
            """;

        public HttpStatusCode EventResponseStatusCode { get; init; } = HttpStatusCode.OK;

        public List<string> EventsResponseJsonSequence { get; init; } = [];

        public HttpStatusCode CreatedEventStatusCode { get; init; } = HttpStatusCode.OK;

        public string CreatedEventJson { get; init; } =
            """{"id":"event-123","htmlLink":"https://calendar.google.com/event?eid=event-123"}""";

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Requests.Add(CloneRequestWithoutContent(request));

            if (request.Content is not null)
            {
                var json = await ReadContentAsStringAsync(request.Content, cancellationToken);
                JsonBodies.Add(JsonDocument.Parse(json));
            }

            var responseStatusCode = HttpStatusCode.OK;
            var responseJson = request.RequestUri?.AbsolutePath switch
            {
                "/calendar/v3/freeBusy" => FreeBusyResponseJson,
                var path when request.Method == HttpMethod.Get
                    && path?.EndsWith("/events", StringComparison.Ordinal) == true => NextEventsResponseJson(),
                var path when request.Method == HttpMethod.Get
                    && path?.Contains("/events/", StringComparison.Ordinal) == true => SetStatusAndReturn(
                        EventResponseStatusCode,
                        EventResponseJson,
                        out responseStatusCode),
                var path when request.Method == HttpMethod.Put
                    && path?.Contains("/events/", StringComparison.Ordinal) == true => CreatedEventJson,
                var path when request.Method == HttpMethod.Delete
                    && path?.Contains("/events/", StringComparison.Ordinal) == true => "{}",
                var path when request.Method == HttpMethod.Post
                    && path?.EndsWith("/events", StringComparison.Ordinal) == true => SetStatusAndReturn(
                        CreatedEventStatusCode,
                        CreatedEventJson,
                        out responseStatusCode),
                _ => "{}",
            };

            return new HttpResponseMessage(responseStatusCode)
            {
                Content = new StringContent(responseJson),
            };
        }

        private string NextEventsResponseJson()
        {
            if (EventsResponseJsonSequence.Count == 0)
            {
                return EventsResponseJson;
            }

            var index = Math.Min(eventsResponseIndex, EventsResponseJsonSequence.Count - 1);
            eventsResponseIndex++;
            return EventsResponseJsonSequence[index];
        }

        private static string SetStatusAndReturn(
            HttpStatusCode statusCode,
            string responseJson,
            out HttpStatusCode responseStatusCode)
        {
            responseStatusCode = statusCode;
            return responseJson;
        }

        private static HttpRequestMessage CloneRequestWithoutContent(HttpRequestMessage request)
        {
            return new HttpRequestMessage(request.Method, request.RequestUri);
        }

        private static async Task<string> ReadContentAsStringAsync(
            HttpContent content,
            CancellationToken cancellationToken)
        {
            if (!content.Headers.ContentEncoding.Contains("gzip"))
            {
                return await content.ReadAsStringAsync(cancellationToken);
            }

            await using var compressed = await content.ReadAsStreamAsync(cancellationToken);
            await using var decompressed = new GZipStream(compressed, CompressionMode.Decompress);
            using var reader = new StreamReader(decompressed);
            return await reader.ReadToEndAsync(cancellationToken);
        }
    }
}
