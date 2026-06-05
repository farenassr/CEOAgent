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
        const string adminKey = "test-admin-key";
        await using var factory = CreateFactory(calendar, adminApiKey: adminKey);
        using var client = factory.CreateClient();
        var companyId = await CreateConfiguredCompanyAsync(factory, client, MvpToolKeys.CheckGoogleCalendarAvailability, adminKey);

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"/v1/admin/companies/{companyId}/google-calendar/availability")
        {
            Content = JsonContent.Create(new
            {
                date = "2026-06-01",
                partySize = 2,
                preferredTime = "16:00:00",
            }),
        };
        request.Headers.Add("X-Company-Id", companyId.ToString());
        request.Headers.Add("X-Admin-Api-Key", adminKey);

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
        const string adminKey = "test-admin-key";
        await using var factory = CreateFactory(calendar, adminApiKey: adminKey);
        using var client = factory.CreateClient();
        var companyId = await CreateConfiguredCompanyAsync(factory, client, MvpToolKeys.CreateGoogleCalendarReservation, adminKey);

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"/v1/admin/companies/{companyId}/google-calendar/reservations")
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
        request.Headers.Add("X-Admin-Api-Key", adminKey);

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
        const string adminKey = "test-admin-key";
        await using var factory = CreateFactory(new RecordingCalendarIntegration(), adminApiKey: adminKey);
        using var client = factory.CreateClient();
        var companyId = await CreateConfiguredCompanyAsync(factory, client, MvpToolKeys.CheckGoogleCalendarAvailability, adminKey);
        var otherCompanyId = await CreateCompanyAsync(client, "Other Company", adminKey);

        var adminOptions = factory.Services.GetRequiredService<Microsoft.Extensions.Options.IOptions<CeoAgent.ApiService.Infrastructure.Security.AdminApiKeyOptions>>().Value;
        adminOptions.CompanyId = otherCompanyId;

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"/v1/admin/companies/{companyId}/google-calendar/availability")
        {
            Content = JsonContent.Create(new
            {
                date = "2026-06-01",
                partySize = 2,
                preferredTime = "16:00:00",
            }),
        };
        request.Headers.Add("X-Company-Id", otherCompanyId.ToString());
        request.Headers.Add("X-Admin-Api-Key", adminKey);

        using var response = await client.SendAsync(request);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task CheckAvailability_WhenGoogleCalendarToolIsMissing_ReturnsBusinessRule()
    {
        const string adminKey = "test-admin-key";
        await using var factory = CreateFactory(new RecordingCalendarIntegration(), adminApiKey: adminKey);
        using var client = factory.CreateClient();
        var companyId = await CreateCompanyAsync(client, "Company Without Tool", adminKey);
        var adminOptions = factory.Services.GetRequiredService<Microsoft.Extensions.Options.IOptions<CeoAgent.ApiService.Infrastructure.Security.AdminApiKeyOptions>>().Value;
        adminOptions.CompanyId = companyId;
        await ConfigureWorkingHoursAsync(client, companyId, adminKey);

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"/v1/admin/companies/{companyId}/google-calendar/availability")
        {
            Content = JsonContent.Create(new
            {
                date = "2026-06-01",
                partySize = 2,
                preferredTime = "16:00:00",
            }),
        };
        request.Headers.Add("X-Company-Id", companyId.ToString());
        request.Headers.Add("X-Admin-Api-Key", adminKey);

        using var response = await client.SendAsync(request);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        problem.ShouldNotBeNull();
        problem.Extensions["code"]?.ToString().ShouldBe("google_calendar_tool_not_configured");
    }

    [Test]
    public async Task EnableGoogleCalendarTool_WhenSlotMinutesIsZero_ReturnsBusinessRule()
    {
        const string adminKey = "test-admin-key";
        await using var factory = CreateFactory(new RecordingCalendarIntegration(), adminApiKey: adminKey);
        using var client = factory.CreateClient();
        var companyId = await CreateCompanyAsync(client, "Invalid Calendar Config Company", adminKey);
        var adminOptions = factory.Services.GetRequiredService<Microsoft.Extensions.Options.IOptions<CeoAgent.ApiService.Infrastructure.Security.AdminApiKeyOptions>>().Value;
        adminOptions.CompanyId = companyId;
        var credentialId = await RegisterGoogleCalendarCredentialAsync(client, companyId, adminKey);

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
            },
            adminKey);

        using var response = await client.SendAsync(request);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        problem.ShouldNotBeNull();
        problem.Extensions["code"]?.ToString().ShouldBe("invalid_slot_minutes");
    }

    [Test]
    public async Task OpenApiDocument_IncludesGoogleCalendarToolEndpointsInDevelopment()
    {
        const string adminKey = "test-admin-key";
        await using var factory = CreateFactory(new RecordingCalendarIntegration(), "Development", adminApiKey: adminKey);
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/openapi/v1.json");
        using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var paths = document.RootElement.GetProperty("paths");
        paths.TryGetProperty("/v1/admin/companies/{companyId}/google-calendar/availability", out _).ShouldBeTrue();
        paths.TryGetProperty("/v1/admin/companies/{companyId}/google-calendar/reservations", out _).ShouldBeTrue();
    }

    private static ApiFactory CreateFactory(
        ICalendarIntegration calendar,
        string environmentName = "Testing",
        string? adminApiKey = null)
    {
        return new ApiFactory(environmentName, services =>
        {
            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(new FixedTimeProvider(new DateTimeOffset(2026, 5, 27, 12, 0, 0, TimeSpan.Zero)));
            services.RemoveAll<ICalendarIntegration>();
            services.AddSingleton(calendar);

            services.Configure<CeoAgent.ApiService.Infrastructure.Security.AdminApiKeyOptions>(options =>
            {
                options.Key = adminApiKey ?? "test-admin-key";
            });
        });
    }

    private static async Task<Guid> CreateConfiguredCompanyAsync(
        ApiFactory factory,
        HttpClient client,
        string toolKey,
        string adminApiKey)
    {
        var companyId = await CreateCompanyAsync(client, "Contoso Bistro", adminApiKey);
        var adminOptions = factory.Services.GetRequiredService<Microsoft.Extensions.Options.IOptions<CeoAgent.ApiService.Infrastructure.Security.AdminApiKeyOptions>>().Value;
        adminOptions.CompanyId = companyId;
        await ConfigureWorkingHoursAsync(client, companyId, adminApiKey);
        var credentialId = await RegisterGoogleCalendarCredentialAsync(client, companyId, adminApiKey);
        await EnableGoogleCalendarToolAsync(client, companyId, credentialId, toolKey, adminApiKey);
        return companyId;
    }

    private static async Task<Guid> CreateCompanyAsync(HttpClient client, string name, string adminApiKey)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/v1/admin/companies")
        {
            Content = JsonContent.Create(new
            {
                name,
                timeZoneId = "America/Bogota",
            })
        };
        request.Headers.Add("X-Admin-Api-Key", adminApiKey);
        using var response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<CompanyResponse>();
        body.ShouldNotBeNull();
        return body.Id;
    }

    private static async Task ConfigureWorkingHoursAsync(HttpClient client, Guid companyId, string adminApiKey)
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
        request.Headers.Add("X-Admin-Api-Key", adminApiKey);

        using var response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();
    }

    private static async Task<Guid> RegisterGoogleCalendarCredentialAsync(HttpClient client, Guid companyId, string adminApiKey)
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
        request.Headers.Add("X-Admin-Api-Key", adminApiKey);

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
        string toolKey,
        string adminApiKey)
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
            },
            adminApiKey);

        using var response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();
    }

    private static HttpRequestMessage CreateEnableGoogleCalendarToolRequest(
        Guid companyId,
        Guid credentialId,
        string toolKey,
        object googleCalendarConfiguration,
        string adminApiKey)
    {
        var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"/v1/admin/companies/{companyId}/tools")
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
        request.Headers.Add("X-Company-Id", companyId.ToString());
        request.Headers.Add("X-Admin-Api-Key", adminApiKey);
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
                },
                required = new[] { "start", "end", "summary" },
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

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow()
        {
            return utcNow;
        }
    }
}
