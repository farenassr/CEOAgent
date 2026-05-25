using CeoAgent.ApiService.Modules.Companies.Mappers;
using CeoAgent.Infrastructure.Entities;
using CeoAgent.Infrastructure.Entities.JsonDocuments;
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
        var companyId = Guid.CreateVersion7();
        var credentialReferenceId = Guid.CreateVersion7();
        var channel = CompanyChannel.ForWhatsAppCloud(
            companyId,
            "123456",
            new WhatsAppCloudMetadata
            {
                BusinessAccountId = "987654321",
                PhoneNumberId = "123456",
            },
            credentialReferenceId);

        var response = CompanyMapper.ToResponse(channel);

        response.Id.ShouldBe(channel.Id);
        response.CompanyId.ShouldBe(companyId);
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
            CompanyId = Guid.CreateVersion7(),
            ModelName = "gpt-4.1-mini",
            DisplayName = "Contoso Assistant",
            Language = "es",
            PromptOverride = "Use a warm tone.",
        };

        var response = CompanyMapper.ToResponse(profile);

        response.Id.ShouldBe(profile.Id);
        response.CompanyId.ShouldBe(profile.CompanyId);
        response.ModelName.ShouldBe(profile.ModelName);
        response.DisplayName.ShouldBe(profile.DisplayName);
        response.Language.ShouldBe(profile.Language);
        response.PromptOverride.ShouldBe(profile.PromptOverride);
    }

    [Test]
    public void ToResponse_MapsCompanyToolEntityToResponse()
    {
        var credentialReferenceId = Guid.CreateVersion7();
        var tool = new CompanyTool
        {
            Id = Guid.CreateVersion7(),
            CompanyId = Guid.CreateVersion7(),
            ToolKey = "check_availability",
            IsEnabled = true,
            CredentialReferenceId = credentialReferenceId,
            Configuration = new CheckAvailabilityConfig
            {
                MinPartySize = 1,
                MaxPartySize = 8,
                SlotMinutes = 30,
                AdvanceBookingDays = 14,
            },
        };

        var response = CompanyMapper.ToResponse(tool);

        response.Id.ShouldBe(tool.Id);
        response.CompanyId.ShouldBe(tool.CompanyId);
        response.ToolKey.ShouldBe(tool.ToolKey);
        response.IsEnabled.ShouldBeTrue();
        response.CredentialReferenceId.ShouldBe(credentialReferenceId);
        response.Configuration.ShouldNotBeNull();
        response.Configuration.Value.GetProperty("MaxPartySize").GetInt32().ShouldBe(8);
    }

    [Test]
    public void ToResponse_MapsIntegrationCredentialReferenceEntityToResponse()
    {
        var credential = new IntegrationCredentialReference
        {
            Id = Guid.CreateVersion7(),
            CompanyId = Guid.CreateVersion7(),
            Provider = "google_calendar",
            Purpose = "calendar",
            Reference = "kv://company/google-calendar",
            Metadata = new GoogleCalendarCredentialMetadata
            {
                CalendarId = "primary",
                Scope = "calendar.events",
            },
        };

        var response = CompanyMapper.ToResponse(credential);

        response.Id.ShouldBe(credential.Id);
        response.CompanyId.ShouldBe(credential.CompanyId);
        response.Provider.ShouldBe(credential.Provider);
        response.Purpose.ShouldBe(credential.Purpose);
        response.Reference.ShouldBe(credential.Reference);
        response.Metadata.ShouldNotBeNull();
        response.Metadata.Value.GetProperty("CalendarId").GetString().ShouldBe("primary");
    }
}
