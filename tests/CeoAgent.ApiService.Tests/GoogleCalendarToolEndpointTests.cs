using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CeoAgent.ApiService.Tests.Support;
using CeoAgent.Integrations.Calendar;
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
        using var client = factory.CreateClient();
        var companyId = await CreateConfiguredCompanyAsync(client, MvpToolKeys.CheckGoogleCalendarAvailability);

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"/v1/tools/companies/{companyId}/google-calendar/availability")
        {
            Content = JsonContent.Create(new
            {
                date = "2026-06-01",
                partySize = 2,
                preferredTime = "16:00:00",
            }),
        };
        request.Headers.Add("X-Company-Id", companyId.ToString());

        using var response = await client.SendAsync(request);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<GoogleCalendarAvailabilityResponse>();
        body.ShouldNotBeNull();
        body.Available.ShouldBeFalse();
        body.AlternativeSlots.ShouldBe([new TimeOnly(16, 30)]);
        body.UnavailabilityReason.ShouldBe("slot_unavailable");

        var calendarRequest = calendar.AvailabilityRequests.Single();
        using var credentialJson = JsonDocument.Parse(calendarRequest.CredentialReference);
        credentialJson.RootElement.GetProperty("type").GetString().ShouldBe("service_account");
        credentialJson.RootElement.GetProperty("project_id").GetString().ShouldBe("gen-lang-client-0728870398");
        credentialJson.RootElement.GetProperty("client_email").GetString().ShouldBe("ceoagent@gen-lang-client-0728870398.iam.gserviceaccount.com");
        calendarRequest.CalendarId.ShouldBe("primary");
        calendarRequest.Start.ShouldBe(new DateTimeOffset(2026, 6, 1, 16, 0, 0, TimeSpan.FromHours(-5)));
        calendarRequest.End.ShouldBe(new DateTimeOffset(2026, 6, 1, 17, 0, 0, TimeSpan.FromHours(-5)));
        calendarRequest.AlternativeSearchStarts.ShouldContain(new DateTimeOffset(2026, 6, 1, 16, 30, 0, TimeSpan.FromHours(-5)));
    }

    [Test]
    public async Task CreateReservation_WithEnabledGoogleCalendarTool_ReturnsCreatedEvent()
    {
        var calendar = new RecordingCalendarIntegration
        {
            ReservationResult = new CalendarReservationResult(
                "event-123",
                "https://calendar.google.com/event?eid=event-123"),
        };
        await using var factory = CreateFactory(calendar);
        using var client = factory.CreateClient();
        var companyId = await CreateConfiguredCompanyAsync(client, MvpToolKeys.CreateGoogleCalendarReservation);

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"/v1/tools/companies/{companyId}/google-calendar/reservations")
        {
            Content = JsonContent.Create(new
            {
                start = "2026-06-01T16:00:00-05:00",
                end = "2026-06-01T17:00:00-05:00",
                summary = "Reservation for 2",
                description = "Window table",
                idempotencyKey = "reservation-123",
            }),
        };
        request.Headers.Add("X-Company-Id", companyId.ToString());

        using var response = await client.SendAsync(request);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<GoogleCalendarReservationResponse>();
        body.ShouldNotBeNull();
        body.EventId.ShouldBe("event-123");
        body.EventUrl.ShouldBe("https://calendar.google.com/event?eid=event-123");

        var calendarRequest = calendar.ReservationRequests.Single();
        using var credentialJson = JsonDocument.Parse(calendarRequest.CredentialReference);
        credentialJson.RootElement.GetProperty("private_key").GetString().ShouldBe("-----BEGIN PRIVATE KEY-----\\nxxx\\n-----END PRIVATE KEY-----\\n");
        calendarRequest.CalendarId.ShouldBe("primary");
        calendarRequest.Summary.ShouldBe("Reservation for 2");
        calendarRequest.Description.ShouldBe("Window table");
        calendarRequest.IdempotencyKey.ShouldBe("reservation-123");
    }

    [Test]
    public async Task CheckAvailability_WhenHeaderCompanyDiffersFromRoute_ReturnsNotFound()
    {
        await using var factory = CreateFactory(new RecordingCalendarIntegration());
        using var client = factory.CreateClient();
        var companyId = await CreateConfiguredCompanyAsync(client, MvpToolKeys.CheckGoogleCalendarAvailability);
        var otherCompanyId = await CreateCompanyAsync(client, "Other Company");

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"/v1/tools/companies/{companyId}/google-calendar/availability")
        {
            Content = JsonContent.Create(new
            {
                date = "2026-06-01",
                partySize = 2,
                preferredTime = "16:00:00",
            }),
        };
        request.Headers.Add("X-Company-Id", otherCompanyId.ToString());

        using var response = await client.SendAsync(request);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task CheckAvailability_WhenGoogleCalendarToolIsMissing_ReturnsBusinessRule()
    {
        await using var factory = CreateFactory(new RecordingCalendarIntegration());
        using var client = factory.CreateClient();
        var companyId = await CreateCompanyAsync(client, "Company Without Tool");
        await ConfigureWorkingHoursAsync(client, companyId);

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"/v1/tools/companies/{companyId}/google-calendar/availability")
        {
            Content = JsonContent.Create(new
            {
                date = "2026-06-01",
                partySize = 2,
                preferredTime = "16:00:00",
            }),
        };
        request.Headers.Add("X-Company-Id", companyId.ToString());

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
        using var client = factory.CreateClient();
        var companyId = await CreateCompanyAsync(client, "Invalid Calendar Config Company");
        var credentialId = await RegisterGoogleCalendarCredentialAsync(client, companyId);

        using var request = CreateEnableGoogleCalendarToolRequest(
            companyId,
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
        paths.TryGetProperty("/v1/tools/companies/{companyId}/google-calendar/availability", out _).ShouldBeTrue();
        paths.TryGetProperty("/v1/tools/companies/{companyId}/google-calendar/reservations", out _).ShouldBeTrue();
    }

    private static ApiFactory CreateFactory(
        ICalendarIntegration calendar,
        string environmentName = "Testing")
    {
        return new ApiFactory(environmentName, services =>
        {
            services.RemoveAll<ICalendarIntegration>();
            services.AddSingleton(calendar);
        });
    }

    private static async Task<Guid> CreateConfiguredCompanyAsync(
        HttpClient client,
        string toolKey)
    {
        var companyId = await CreateCompanyAsync(client, "Contoso Bistro");
        await ConfigureWorkingHoursAsync(client, companyId);
        var credentialId = await RegisterGoogleCalendarCredentialAsync(client, companyId);
        await EnableGoogleCalendarToolAsync(client, companyId, credentialId, toolKey);
        return companyId;
    }

    private static async Task<Guid> CreateCompanyAsync(HttpClient client, string name)
    {
        using var response = await client.PostAsJsonAsync("/v1/admin/companies", new
        {
            name,
            timeZoneId = "America/Bogota",
        });
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<CompanyResponse>();
        body.ShouldNotBeNull();
        return body.Id;
    }

    private static async Task ConfigureWorkingHoursAsync(HttpClient client, Guid companyId)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"/v1/admin/companies/{companyId}/agent-profile")
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
        request.Headers.Add("X-Company-Id", companyId.ToString());

        using var response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();
    }

    private static async Task<Guid> RegisterGoogleCalendarCredentialAsync(HttpClient client, Guid companyId)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"/v1/admin/companies/{companyId}/integration-credentials")
        {
            Content = JsonContent.Create(new
            {
                provider = "google_calendar",
                purpose = "google_calendar",
                reference = "stored://google-calendar/service-account",
                metadata = new
                {
                    provider = "google_calendar",
                    google_calendar = new
                    {
                        calendarId = "primary",
                        scope = "https://www.googleapis.com/auth/calendar",
                        type = "service_account",
                        project_id = "gen-lang-client-0728870398",
                        private_key_id = "private-key-id",
                        private_key = "-----BEGIN PRIVATE KEY-----\\nxxx\\n-----END PRIVATE KEY-----\\n",
                        client_email = "ceoagent@gen-lang-client-0728870398.iam.gserviceaccount.com",
                        client_id = "1111",
                        auth_uri = "https://accounts.google.com/o/oauth2/auth",
                        token_uri = "https://oauth2.googleapis.com/token",
                        auth_provider_x509_cert_url = "https://www.googleapis.com/oauth2/v1/certs",
                        client_x509_cert_url = "https://www.googleapis.com/robot/v1/metadata/x509/ceoagent%40gen-lang-client-0728870398.iam.gserviceaccount.com",
                        universe_domain = "googleapis.com",
                    },
                },
            }),
        };
        request.Headers.Add("X-Company-Id", companyId.ToString());

        using var response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<IntegrationCredentialResponse>();
        body.ShouldNotBeNull();
        return body.Id;
    }

    private static async Task EnableGoogleCalendarToolAsync(
        HttpClient client,
        Guid companyId,
        Guid credentialId,
        string toolKey)
    {
        using var request = CreateEnableGoogleCalendarToolRequest(
            companyId,
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
        Guid companyId,
        Guid credentialId,
        string toolKey,
        object googleCalendarConfiguration)
    {
        var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"/v1/admin/companies/{companyId}/tools")
        {
            Content = JsonContent.Create(new
            {
                toolKey,
                isEnabled = true,
                credentialReferenceId = credentialId,
                configuration = new
                {
                    toolKey = "google_calendar",
                    google_calendar = googleCalendarConfiguration,
                },
            }),
        };
        request.Headers.Add("X-Company-Id", companyId.ToString());
        return request;
    }

    private sealed class RecordingCalendarIntegration : ICalendarIntegration
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
}
