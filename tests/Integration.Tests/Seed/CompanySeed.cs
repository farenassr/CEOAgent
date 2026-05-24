using CEOAgent.Infrastructure.Entities;
using CompanyEntity = CEOAgent.Infrastructure.Entities.Company;
using CEOAgent.Infrastructure;

namespace Integration.Tests.Seed;

internal static class CompanySeed
{
    public static async Task SeedChannelsAsync(CEOAgentDbContext dbContext, Guid companyA, Guid companyB)
    {
        dbContext.CompanyChannels.AddRange(
            new CompanyChannel
            {
                CompanyId = companyA,
                Provider = "whatsapp_cloud",
                ProviderChannelId = "company-a-channel",
            },
            new CompanyChannel
            {
                CompanyId = companyB,
                Provider = "whatsapp_cloud",
                ProviderChannelId = "company-b-channel",
            });

        await dbContext.SaveChangesAsync();
    }

    public static async Task<CompanySeedIds> SeedCompanyGraphAsync(
        CEOAgentDbContext dbContext,
        Guid companyId,
        string providerChannelId)
    {
        var channelId = Guid.CreateVersion7();
        var agentProfileId = Guid.CreateVersion7();
        var customerId = Guid.CreateVersion7();
        var toolId = Guid.CreateVersion7();

        dbContext.Companies.Add(new CompanyEntity
        {
            Id = companyId,
            Name = "Contoso Bistro",
            TimeZoneId = "America/Bogota",
        });
        dbContext.CompanyChannels.Add(new CompanyChannel
        {
            Id = channelId,
            CompanyId = companyId,
            Provider = "whatsapp_cloud",
            ProviderChannelId = providerChannelId,
        });
        dbContext.AgentProfiles.Add(new AgentProfile
        {
            Id = agentProfileId,
            CompanyId = companyId,
            ModelName = "gpt-4.1-mini",
            DisplayName = "Contoso Assistant",
            Language = "es",
        });
        dbContext.Customers.Add(new Customer
        {
            Id = customerId,
            CompanyId = companyId,
            CompanyChannelId = channelId,
            ExternalCustomerId = "573001112233",
        });
        dbContext.CompanyTools.Add(new CompanyTool
        {
            Id = toolId,
            CompanyId = companyId,
            ToolKey = "request_human_handoff",
        });

        await dbContext.SaveChangesAsync();

        return new CompanySeedIds(channelId, agentProfileId, customerId, toolId);
    }
}

internal sealed record CompanySeedIds(Guid ChannelId, Guid AgentProfileId, Guid CustomerId, Guid ToolId);
