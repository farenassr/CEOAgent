using CeoAgent.ApiService.Modules.Companies.Mappers;
using CeoAgent.Infrastructure.Entities;
using CeoAgent.Infrastructure.Entities.JsonDocuments;
using CeoAgent.Shared.Enums;
using CeoAgent.Shared.Request.Company;
using System.Text.Json;
using Shouldly;

namespace CeoAgent.ApiService.Tests;

public sealed class CompanyMapperTests
{
    /// <summary>
    /// Verifies that the Companies module owns entity-to-response mapping for company onboarding.
    /// </summary>
    [Test]
    public void ToResponse_MapsCompanyEntityToResponse()
    {
        var company = new Company
        {
            Id = Guid.CreateVersion7(),
            Name = "Contoso Bistro",
            TimeZoneId = "America/Bogota",
        };

        var response = CompanyMapper.ToResponse(company);

        response.Id.ShouldBe(company.Id);
        response.Name.ShouldBe(company.Name);
        response.Status.ShouldBe(company.Status.ToString());
        response.TimeZoneId.ShouldBe(company.TimeZoneId);
        response.WorkingHours.ShouldBeNull();
    }

    [Test]
    public void ToResponse_MapsCompanyChannelEntityToResponse()
    {
        var organizationId = Guid.CreateVersion7();
        var credentialReferenceId = Guid.CreateVersion7();
        var channel = CompanyChannel.ForWhatsAppCloud(
            organizationId,
            "123456",
            new WhatsAppCloudMetadata
            {
                BusinessAccountId = "987654321",
                PhoneNumberId = "123456",
            },
            credentialReferenceId);

        var response = CompanyMapper.ToResponse(channel);

        response.Id.ShouldBe(channel.Id);
        response.OrganizationId.ShouldBe(organizationId);
        response.Provider.ShouldBe(channel.Provider);
        response.ProviderChannelId.ShouldBe("123456");
        response.CredentialReferenceId.ShouldBe(credentialReferenceId);
        response.Metadata.ShouldNotBeNull();
        response.Metadata.Value.GetProperty("whatsapp_cloud").GetProperty("phone_number_id").GetString().ShouldBe("123456");
    }

    [Test]
    public void ToResponse_MapsAgentProfileEntityToResponse()
    {
        var profile = new AgentProfile
        {
            Id = Guid.CreateVersion7(),
            OrganizationId = Guid.CreateVersion7(),
            ModelName = "gpt-4.1-mini",
            LlmProvider = LlmProvider.OpenAI,
            DisplayName = "Contoso Assistant",
            Language = "es",
            PromptOverride = "Use a warm tone.",
        };

        var response = CompanyMapper.ToResponse(profile);

        response.Id.ShouldBe(profile.Id);
        response.OrganizationId.ShouldBe(profile.OrganizationId);
        response.ModelName.ShouldBe(profile.ModelName);
        response.LlmProvider.ShouldBe(profile.LlmProvider);
        response.DisplayName.ShouldBe(profile.DisplayName);
        response.Language.ShouldBe(profile.Language);
        response.PromptOverride.ShouldBe(profile.PromptOverride);
    }

    [Test]
    public void ApplyToEntity_MapsAgentProfileProviderToEntity()
    {
        var company = new Company
        {
            Id = Guid.CreateVersion7(),
            Name = "Contoso Bistro",
            TimeZoneId = "America/Bogota",
        };
        var profile = new AgentProfile
        {
            OrganizationId = company.Id,
            ModelName = "gpt-4.1-mini",
            DisplayName = "Contoso Assistant",
            Language = "es",
        };
        var request = new AgentProfileRequest
        {
            ModelName = "gpt-5.5",
            LlmProvider = LlmProvider.OpenAI,
            DisplayName = "Contoso AI",
            Language = "es",
            TimeZoneId = "America/Bogota",
        };

        CompanyMapper.ApplyToEntity(request, profile, company);

        profile.ModelName.ShouldBe("gpt-5.5");
        profile.LlmProvider.ShouldBe(LlmProvider.OpenAI);
    }

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
    public void ApplyToEntity_MapsCompanyToolRequestToEntity()
    {
        var credentialReferenceId = Guid.CreateVersion7();
        var tool = new CompanyTool
        {
            OrganizationId = Guid.CreateVersion7(),
            ToolKey = "check_availability",
        };
        var request = new CompanyToolRequest
        {
            Description = "Checks available reservation slots.",
            ParametersSchema = ParseSchema("""{"type":"object","properties":{"date":{"type":"string"}},"required":["date"],"additionalProperties":false}"""),
            IsEnabled = false,
            CredentialReferenceId = credentialReferenceId,
        };

        CompanyMapper.ApplyToEntity(request, tool);

        tool.Description.ShouldBe(request.Description);
        tool.ParametersSchema.ShouldNotBeNull();
        tool.ParametersSchema.Value.GetProperty("required")[0].GetString().ShouldBe("date");
        tool.IsEnabled.ShouldBeFalse();
        tool.CredentialReferenceId.ShouldBe(credentialReferenceId);
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
