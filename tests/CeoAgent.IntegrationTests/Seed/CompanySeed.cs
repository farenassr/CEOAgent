using CeoAgent.Infrastructure.Entities;
using CeoAgent.Infrastructure.Entities.JsonDocuments;
using CompanyEntity = CeoAgent.Infrastructure.Entities.Company;
using CeoAgent.Infrastructure;

namespace CeoAgent.IntegrationTests.Seed;

internal static class CompanySeed
{
    public static async Task SeedChannelsAsync(CeoAgentDbContext dbContext, Guid companyA, Guid companyB)
    {
        dbContext.CompanyChannels.AddRange(
            CompanyChannel.ForWhatsAppCloud(companyA, "company-a-channel", CreateWhatsAppMetadata("company-a-channel")),
            CompanyChannel.ForWhatsAppCloud(companyB, "company-b-channel", CreateWhatsAppMetadata("company-b-channel")));

        await dbContext.SaveChangesAsync();
    }

    public static async Task<CompanySeedIds> SeedCompanyGraphAsync(
        CeoAgentDbContext dbContext,
        Guid organizationId,
        string providerChannelId)
    {
        var channelId = Guid.CreateVersion7();
        var agentProfileId = Guid.CreateVersion7();
        var customerId = Guid.CreateVersion7();
        var toolId = Guid.CreateVersion7();

        dbContext.Companies.Add(new CompanyEntity
        {
            Id = organizationId,
            Name = "Contoso Bistro",
            TimeZoneId = "America/Bogota",
        });
        dbContext.CompanyChannels.Add(CompanyChannel.ForWhatsAppCloud(
            organizationId,
            providerChannelId,
            CreateWhatsAppMetadata(providerChannelId),
            id: channelId));
        dbContext.AgentProfiles.Add(new AgentProfile
        {
            Id = agentProfileId,
            OrganizationId = organizationId,
            ModelName = "gpt-4.1-mini",
            DisplayName = "Contoso Assistant",
            Language = "es",
        });
        dbContext.Customers.Add(new Customer
        {
            Id = customerId,
            OrganizationId = organizationId,
            CompanyChannelId = channelId,
            ExternalCustomerId = "573001112233",
        });
        dbContext.CompanyTools.Add(new CompanyTool
        {
            Id = toolId,
            OrganizationId = organizationId,
            ToolKey = "request_human_handoff",
        });

        await dbContext.SaveChangesAsync();

        return new CompanySeedIds(channelId, agentProfileId, customerId, toolId);
    }

    private static WhatsAppCloudMetadata CreateWhatsAppMetadata(string phoneNumberId)
    {
        return new WhatsAppCloudMetadata
        {
            BusinessAccountId = "987654321",
            PhoneNumberId = phoneNumberId,
        };
    }
}

internal sealed record CompanySeedIds(Guid ChannelId, Guid AgentProfileId, Guid CustomerId, Guid ToolId);
