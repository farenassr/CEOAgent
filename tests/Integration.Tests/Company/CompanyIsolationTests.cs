using CEOAgent.Application.Company;
using CEOAgent.Infrastructure.Persistence;
using CEOAgent.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Shouldly;

namespace Integration.Tests.Company;

public sealed class CompanyIsolationTests
{
    [Test]
    public async Task CompanyQueryFilter_WhenCompanyAContext_DoesNotReturnCompanyBRows()
    {
        var companyA = Guid.CreateVersion7();
        var companyB = Guid.CreateVersion7();
        var companyContext = new CompanyContextAccessor();
        await using var dbContext = CreateDbContext(companyContext);
        await SeedChannelsAsync(dbContext, companyA, companyB);

        companyContext.SetCompany(companyA);

        var channels = await dbContext.CompanyChannels.Select(entity => entity.ProviderChannelId).ToListAsync();

        channels.ShouldBe(["company-a-channel"]);
    }

    [Test]
    public async Task CompanyQueryFilter_WhenCompanyBContext_DoesNotReturnCompanyARows()
    {
        var companyA = Guid.CreateVersion7();
        var companyB = Guid.CreateVersion7();
        var companyContext = new CompanyContextAccessor();
        await using var dbContext = CreateDbContext(companyContext);
        await SeedChannelsAsync(dbContext, companyA, companyB);

        companyContext.SetCompany(companyB);

        var channels = await dbContext.CompanyChannels.Select(entity => entity.ProviderChannelId).ToListAsync();

        channels.ShouldBe(["company-b-channel"]);
    }

    [Test]
    public async Task CompanyQueryFilter_WhenCompanyContextMissing_ReturnsNoCompanyOwnedRows()
    {
        var companyA = Guid.CreateVersion7();
        var companyB = Guid.CreateVersion7();
        var companyContext = new CompanyContextAccessor();
        await using var dbContext = CreateDbContext(companyContext);
        await SeedChannelsAsync(dbContext, companyA, companyB);

        companyContext.Clear();

        var channels = await dbContext.CompanyChannels.ToListAsync();

        channels.ShouldBeEmpty();
    }

    private static CEOAgentDbContext CreateDbContext(ICompanyContext companyContext)
    {
        var options = new DbContextOptionsBuilder<CEOAgentDbContext>()
            .UseInMemoryDatabase($"company-isolation-tests-{Guid.CreateVersion7()}")
            .Options;

        return new CEOAgentDbContext(options, companyContext, TimeProvider.System);
    }

    private static async Task SeedChannelsAsync(CEOAgentDbContext dbContext, Guid companyA, Guid companyB)
    {
        dbContext.CompanyChannels.AddRange(
            new CompanyChannel
            {
                CompanyId = companyA,
                Provider = "whatsapp_cloud",
                ProviderChannelId = "company-a-channel"
            },
            new CompanyChannel
            {
                CompanyId = companyB,
                Provider = "whatsapp_cloud",
                ProviderChannelId = "company-b-channel"
            });

        await dbContext.SaveChangesAsync();
    }
}
