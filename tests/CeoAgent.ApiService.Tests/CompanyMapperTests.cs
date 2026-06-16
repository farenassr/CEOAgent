using CeoAgent.ApiService.Modules.Companies.Mappers;
using CeoAgent.Infrastructure.Entities;
using CeoAgent.Infrastructure.Entities.JsonDocuments;
using CeoAgent.Shared.Enums;
using System.Text.Json;
using Shouldly;

namespace CeoAgent.ApiService.Tests;

public sealed class CompanyMapperTests
{
    [Test]
    public void ToResponse_MapsCompanyToolEntityToResponse()
    {
        var credentialReferenceId = Guid.CreateVersion7();
        var tool = new CompanyTool
        {
            Id = Guid.CreateVersion7(),
            OrganizationId = Guid.CreateVersion7(),
            ToolKey = "check_availability",
            Description = "Checks available reservation slots.",
            ParametersSchema = ParseSchema("""{"type":"object","properties":{"date":{"type":"string"}},"required":["date"],"additionalProperties":false}"""),
            IsEnabled = true,
            CredentialReferenceId = credentialReferenceId,
            Configuration = ToolConfiguration.ForCheckAvailability(new CheckAvailabilityConfig
            {
                MinPartySize = 1,
                MaxPartySize = 8,
                SlotMinutes = 30,
                AdvanceBookingDays = 14,
            }),
        };

        var response = CompanyMapper.ToResponse(tool);

        response.Id.ShouldBe(tool.Id);
        response.OrganizationId.ShouldBe(tool.OrganizationId);
        response.ToolKey.ShouldBe(tool.ToolKey);
        response.Description.ShouldBe(tool.Description);
        response.ParametersSchema.ShouldNotBeNull();
        response.ParametersSchema.Value.GetProperty("properties").GetProperty("date").GetProperty("type").GetString().ShouldBe("string");
        response.IsEnabled.ShouldBeTrue();
        response.CredentialReferenceId.ShouldBe(credentialReferenceId);
        response.Configuration.ShouldNotBeNull();
        response.Configuration.Value.GetProperty("toolKey").GetString().ShouldBe("check_availability");
        response.Configuration.Value.GetProperty("check_availability").GetProperty("maxPartySize").GetInt32().ShouldBe(8);
    }

    [Test]
    public void ToResponse_MapsIntegrationCredentialReferenceEntityToResponse()
    {
        var credential = new IntegrationCredentialReference
        {
            Id = Guid.CreateVersion7(),
            OrganizationId = Guid.CreateVersion7(),
            Provider = IntegrationProvider.GoogleCalendar,
            Purpose = "calendar",
            Reference = "kv://company/google-calendar",
            Metadata = CredentialMetadata.ForGoogleCalendar(new GoogleCalendarCredentialMetadata
            {
                CalendarId = "primary",
                Scope = "calendar.events",
                ExpiresAt = new DateTimeOffset(2026, 6, 8, 12, 0, 0, TimeSpan.Zero),
            }),
        };

        var response = CompanyMapper.ToResponse(credential);

        response.Id.ShouldBe(credential.Id);
        response.OrganizationId.ShouldBe(credential.OrganizationId);
        response.Provider.ShouldBe(credential.Provider);
        response.Purpose.ShouldBe(credential.Purpose);
        response.Reference.ShouldBe(credential.Reference);
        response.Metadata.ShouldNotBeNull();
        response.Metadata.Value.GetProperty("provider").GetString().ShouldBe("google_calendar");
        response.Metadata.Value.GetProperty("google_calendar").GetProperty("calendarId").GetString().ShouldBe("primary");
        response.Metadata.Value.GetProperty("google_calendar").TryGetProperty("private_key", out _).ShouldBeFalse();
        response.Metadata.Value.GetProperty("google_calendar").TryGetProperty("client_email", out _).ShouldBeFalse();
    }

    private static JsonElement ParseSchema(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }
}
