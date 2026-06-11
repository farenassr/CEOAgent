using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CeoAgent.ApiService.Tests.Support;
using CeoAgent.Application.Abstractions.AITools.GoogleCalendar;
using CeoAgent.Shared.Calendar;
using CeoAgent.Shared.Constants;
using CeoAgent.Shared.Response.Company;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Shouldly;

namespace CeoAgent.ApiService.Tests;

[NotInParallel]
public sealed class GoogleCalendarToolEndpointTests
{
    [Test]
    public async Task CheckAvailability_WithEnabledGoogleCalendarTool_ReturnsCalendarAvailability()
    {
        var calendar = new RecordingCalendarIntegration
        {
            AvailabilityResult = new CalendarAvailabilityResult(
                Available: false,
                AlternativeStarts:
                [
                    new DateTimeOffset(2026, 6, 1, 16, 30, 0, TimeSpan.FromHours(-5)),
                ],
                UnavailabilityReason: "slot_unavailable"),
        };
        await using var factory = CreateFactory(calendar);
        using var client = factory.CreateAuthenticatedClient();
        var organizationId = await CreateConfiguredCompanyAsync(client, MvpToolKeys.CheckGoogleCalendarAvailability);

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"/v1/admin/companies/{organizationId}/google-calendar/availability")
        {
            Content = JsonContent.Create(new
            {
                date = "2026-06-01",
                partySize = 2,
                preferredTime = "16:00:00",
            }),
        };
        using var response = await client.SendAsync(request);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<GoogleCalendarAvailabilityResponse>();
        body.ShouldNotBeNull();
        body.Available.ShouldBeFalse();
        body.AlternativeSlots.ShouldBe([new TimeOnly(16, 30)]);
        body.UnavailabilityReason.ShouldBe("slot_unavailable");

        var calendarRequest = calendar.AvailabilityRequests.Single();
        calendarRequest.CredentialReference.ShouldBe("kv://google-calendar/contoso/service-account");
        calendarRequest.CalendarId.ShouldBe("primary");
        calendarRequest.Start.ShouldBe(new DateTimeOffset(2026, 6, 1, 16, 0, 0, TimeSpan.FromHours(-5)));
        calendarRequest.End.ShouldBe(new DateTimeOffset(2026, 6, 1, 17, 0, 0, TimeSpan.FromHours(-5)));
        calendarRequest.SearchWindowStart.ShouldBe(new DateTimeOffset(2026, 6, 1, 13, 0, 0, TimeSpan.FromHours(-5)));
        calendarRequest.SearchWindowEnd.ShouldBe(new DateTimeOffset(2026, 6, 1, 19, 0, 0, TimeSpan.FromHours(-5)));
        calendarRequest.AlternativeSearchStarts.ShouldContain(new DateTimeOffset(2026, 6, 1, 16, 30, 0, TimeSpan.FromHours(-5)));
    }

    [Test]
    public async Task CreateReservation_WithEnabledGoogleCalendarTool_ReturnsBusinessRule()
    {
        var calendar = new RecordingCalendarIntegration
        {
            ReservationResult = new CalendarReservationResult(
                "event-123",
                "https://calendar.google.com/event?eid=event-123"),
        };
        await using var factory = CreateFactory(calendar);
        using var client = factory.CreateAuthenticatedClient();
        var organizationId = await CreateConfiguredCompanyAsync(client, MvpToolKeys.CreateGoogleCalendarReservation);

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"/v1/admin/companies/{organizationId}/google-calendar/reservations")
        {
            Content = JsonContent.Create(new
            {
                start = "2026-06-01T16:00:00-05:00",
                end = "2026-06-01T17:00:00-05:00",
                summary = "Reservation for 2",
                description = "Window table",
                customerName = "Ada Lovelace",
                idempotencyKey = "reservation-123",
            }),
        };
        using var response = await client.SendAsync(request);

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        problem.ShouldNotBeNull();
        problem.Extensions["code"]?.ToString().ShouldBe("admin_google_calendar_mutation_disabled");
        calendar.ReservationRequests.ShouldBeEmpty();
    }

    [Test]
    public async Task CheckAvailability_WhenJwtCompanyDiffersFromRoute_ReturnsNotFound()
    {
        await using var factory = CreateFactory(new RecordingCalendarIntegration());
        using var client = factory.CreateAuthenticatedClient();
        var organizationId = await CreateConfiguredCompanyAsync(client, MvpToolKeys.CheckGoogleCalendarAvailability);
        var otherOrganizationId = Guid.CreateVersion7();
        client.DefaultRequestHeaders.Authorization = TestAuthentication.OrganizationBearer(otherOrganizationId);
        otherOrganizationId = await CreateCompanyAsync(client, "Other Company");
        client.DefaultRequestHeaders.Authorization = TestAuthentication.OrganizationBearer(otherOrganizationId);

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"/v1/admin/companies/{organizationId}/google-calendar/availability")
        {
            Content = JsonContent.Create(new
            {
                date = "2026-06-01",
                partySize = 2,
                preferredTime = "16:00:00",
            }),
        };
        using var response = await client.SendAsync(request);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task CheckAvailability_WhenGoogleCalendarToolIsMissing_ReturnsBusinessRule()
    {
        await using var factory = CreateFactory(new RecordingCalendarIntegration());
        using var client = factory.CreateAuthenticatedClient();
        var organizationId = await CreateCompanyAsync(client, "Company Without Tool");
        client.DefaultRequestHeaders.Authorization = TestAuthentication.OrganizationBearer(organizationId);
        await ConfigureWorkingHoursAsync(client, organizationId);

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"/v1/admin/companies/{organizationId}/google-calendar/availability")
        {
            Content = JsonContent.Create(new
            {
                date = "2026-06-01",
                partySize = 2,
                preferredTime = "16:00:00",
            }),
        };
        using var response = await client.SendAsync(request);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        problem.ShouldNotBeNull();
        problem.Extensions["code"]?.ToString().ShouldBe("google_calendar_tool_not_configured");
    }

    [Test]
    public async Task EnableGoogleCalendarTool_WhenSlotMinutesIsZero_ReturnsBusinessRule()
    {
        await using var factory = CreateFactory(new RecordingCalendarIntegration());
        using var client = factory.CreateAuthenticatedClient();
        var organizationId = await CreateCompanyAsync(client, "Invalid Calendar Config Company");
        client.DefaultRequestHeaders.Authorization = TestAuthentication.OrganizationBearer(organizationId);
        var credentialId = await RegisterGoogleCalendarCredentialAsync(client, organizationId);

        using var request = CreateEnableGoogleCalendarToolRequest(
            organizationId,
            credentialId,
            MvpToolKeys.CheckGoogleCalendarAvailability,
            new
            {
                calendarId = "primary",
                timeZoneId = "America/Bogota",
                bufferMinutes = 0,
                reservationMinutes = 60,
                advanceBookingDays = 14,
                slotMinutes = 0,
            });

        using var response = await client.SendAsync(request);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        problem.ShouldNotBeNull();
        problem.Extensions["code"]?.ToString().ShouldBe("invalid_slot_minutes");
    }

    [Test]
    public async Task OpenApiDocument_IncludesGoogleCalendarToolEndpointsInDevelopment()
    {
        await using var factory = CreateFactory(new RecordingCalendarIntegration(), "Development");
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/openapi/v1.json");
        using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var paths = document.RootElement.GetProperty("paths");
        paths.TryGetProperty("/v1/admin/companies/{organizationId}/google-calendar/availability", out _).ShouldBeTrue();
        paths.TryGetProperty("/v1/admin/companies/{organizationId}/google-calendar/reservations", out _).ShouldBeTrue();
    }

    private static ApiFactory CreateFactory(
        IGoogleCalendarIntegration calendar,
        string environmentName = "Testing")
    {
        return new ApiFactory(environmentName, services =>
        {
            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(new FixedTimeProvider(new DateTimeOffset(2026, 5, 27, 12, 0, 0, TimeSpan.Zero)));
            services.RemoveAll<IGoogleCalendarIntegration>();
            services.AddSingleton(calendar);
        });
    }

    private static async Task<Guid> CreateConfiguredCompanyAsync(
        HttpClient client,
        string toolKey)
    {
        var organizationId = await CreateCompanyAsync(client, "Contoso Bistro");
        client.DefaultRequestHeaders.Authorization = TestAuthentication.OrganizationBearer(organizationId);
        await ConfigureWorkingHoursAsync(client, organizationId);
        var credentialId = await RegisterGoogleCalendarCredentialAsync(client, organizationId);
        await EnableGoogleCalendarToolAsync(client, organizationId, credentialId, toolKey);
        return organizationId;
    }

    private static async Task<Guid> CreateCompanyAsync(HttpClient client, string name)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/v1/admin/companies")
        {
            Content = JsonContent.Create(new
            {
                name,
                timeZoneId = "America/Bogota",
            })
        };
        using var response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<CompanyResponse>();
        body.ShouldNotBeNull();
        return body.Id;
    }

    private static async Task ConfigureWorkingHoursAsync(HttpClient client, Guid organizationId)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"/v1/admin/companies/{organizationId}/agent-profile")
        {
            Content = JsonContent.Create(new
            {
                modelName = "gpt-4.1-mini",
                displayName = "Contoso Assistant",
                language = "es",
                timeZoneId = "America/Bogota",
                workingHours = new
                {
                    schedule = new
                    {
                        monday = new[]
                        {
                            new
                            {
                                start = "12:00:00",
                                end = "22:00:00",
                            },
                        },
                    },
                    holidays = Array.Empty<object>(),
                },
            }),
        };
        using var response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();
    }

    private static async Task<Guid> RegisterGoogleCalendarCredentialAsync(HttpClient client, Guid organizationId)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"/v1/admin/companies/{organizationId}/integration-credentials")
        {
            Content = JsonContent.Create(new
            {
                provider = "google_calendar",
                purpose = "google_calendar",
                reference = "kv://google-calendar/contoso/service-account",
                metadata = new
                {
                    provider = "google_calendar",
                    google_calendar = new
                    {
                        calendarId = "primary",
                        scope = "https://www.googleapis.com/auth/calendar",
                    },
                },
            }),
        };
        using var response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<IntegrationCredentialResponse>();
        body.ShouldNotBeNull();
        return body.Id;
    }

    private static async Task EnableGoogleCalendarToolAsync(
        HttpClient client,
        Guid organizationId,
        Guid credentialId,
        string toolKey)
    {
        using var request = CreateEnableGoogleCalendarToolRequest(
            organizationId,
            credentialId,
            toolKey,
            new
            {
                calendarId = "primary",
                timeZoneId = "America/Bogota",
                bufferMinutes = 0,
            });

        using var response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();
    }

    private static HttpRequestMessage CreateEnableGoogleCalendarToolRequest(
        Guid organizationId,
        Guid credentialId,
        string toolKey,
        object googleCalendarConfiguration)
    {
        var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"/v1/admin/companies/{organizationId}/tools")
        {
            Content = JsonContent.Create(new
            {
                toolKey,
                description = ToolDescription(toolKey),
                parametersSchema = ToolSchema(toolKey),
                isEnabled = true,
                credentialReferenceId = credentialId,
                configuration = new
                {
                    toolKey = "google_calendar",
                    google_calendar = googleCalendarConfiguration,
                },
            }),
        };
        return request;
    }

    private static string ToolDescription(string toolKey)
    {
        return toolKey == MvpToolKeys.CreateGoogleCalendarReservation
            ? "Create a Google Calendar reservation after explicit customer confirmation."
            : "Check Google Calendar availability before offering or confirming reservation times.";
    }

    private static object ToolSchema(string toolKey)
    {
        if (toolKey == MvpToolKeys.CreateGoogleCalendarReservation)
        {
            return new
            {
                type = "object",
                properties = new
                {
                    start = new { type = "string" },
                    end = new { type = "string" },
                    summary = new { type = "string" },
                    customerName = new { type = "string" },
                },
                required = new[] { "start", "end", "summary", "customerName" },
                additionalProperties = false,
            };
        }

        return new
        {
            type = "object",
            properties = new
            {
                date = new { type = "string" },
                partySize = new { type = "integer" },
                preferredTime = new { type = new object[] { "string", "null" } },
            },
            required = new[] { "date", "partySize", "preferredTime" },
            additionalProperties = false,
        };
    }

    private sealed class RecordingCalendarIntegration : IGoogleCalendarIntegration
    {
        public List<CalendarAvailabilityRequest> AvailabilityRequests { get; } = [];

        public List<CalendarReservationRequest> ReservationRequests { get; } = [];

        public CalendarAvailabilityResult AvailabilityResult { get; init; } = new(
            Available: true,
            AlternativeStarts: [],
            UnavailabilityReason: null);

        public CalendarReservationResult ReservationResult { get; init; } = new(
            "event-123",
            "https://calendar.google.com/event?eid=event-123");

        public Task<CalendarAvailabilityResult> CheckAvailabilityAsync(
            CalendarAvailabilityRequest request,
            CancellationToken cancellationToken)
        {
            AvailabilityRequests.Add(request);
            return Task.FromResult(AvailabilityResult);
        }

        public Task<CalendarReservationResult> CreateReservationAsync(
            CalendarReservationRequest request,
            CancellationToken cancellationToken)
        {
            ReservationRequests.Add(request);
            return Task.FromResult(ReservationResult);
        }

        public Task<CalendarReservationSearchResult> FindReservationsAsync(
            CalendarReservationSearchRequest request,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(new CalendarReservationSearchResult([]));
        }

        public Task<CalendarReservationMutationResult> UpdateReservationAsync(
            CalendarReservationUpdateRequest request,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(CalendarReservationMutationResult.NotOwned(request.ReservationId));
        }

        public Task<CalendarReservationCancellationResult> CancelReservationAsync(
            CalendarReservationCancellationRequest request,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(CalendarReservationCancellationResult.NotOwned(request.ReservationId));
        }
    }

    private sealed class GoogleCalendarAvailabilityResponse
    {
        public bool Available { get; set; }

        public List<TimeOnly> AlternativeSlots { get; set; } = [];

        public string? UnavailabilityReason { get; set; }
    }

    private sealed class GoogleCalendarReservationResponse
    {
        public string EventId { get; set; } = string.Empty;

        public string EventUrl { get; set; } = string.Empty;
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow()
        {
            return utcNow;
        }
    }
}
