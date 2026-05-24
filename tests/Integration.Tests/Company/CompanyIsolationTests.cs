using CEOAgent.Application.Company;
using CEOAgent.Infrastructure.Persistence;
using Integration.Tests.Infrastructure;
using Integration.Tests.Seed;
using Microsoft.EntityFrameworkCore;
using Shouldly;

namespace Integration.Tests.Company;

public sealed class CompanyIsolationTests
{
    /// <summary>
    /// Verifies that company query filters only return Company A rows when Company A is the ambient company.
    /// </summary>
    [Test]
    public async Task CompanyQueryFilter_WhenCompanyAContext_DoesNotReturnCompanyBRows()
    {
        var companyA = Guid.CreateVersion7();
        var companyB = Guid.CreateVersion7();
        var companyContext = new CompanyContextAccessor();
        await using var dbContext = CEOAgentDbContextTestFactory.CreateInMemory(companyContext);
        await CompanySeed.SeedChannelsAsync(dbContext, companyA, companyB);

        companyContext.SetCompany(companyA);

        var channels = await dbContext.CompanyChannels
            .WithDefaultTracking()
            .Select(entity => entity.ProviderChannelId)
            .ToListAsync();

        channels.ShouldBe(["company-a-channel"]);
    }

    /// <summary>
    /// Verifies that company query filters only return Company B rows when Company B is the ambient company.
    /// </summary>
    [Test]
    public async Task CompanyQueryFilter_WhenCompanyBContext_DoesNotReturnCompanyARows()
    {
        var companyA = Guid.CreateVersion7();
        var companyB = Guid.CreateVersion7();
        var companyContext = new CompanyContextAccessor();
        await using var dbContext = CEOAgentDbContextTestFactory.CreateInMemory(companyContext);
        await CompanySeed.SeedChannelsAsync(dbContext, companyA, companyB);

        companyContext.SetCompany(companyB);

        var channels = await dbContext.CompanyChannels
            .WithDefaultTracking()
            .Select(entity => entity.ProviderChannelId)
            .ToListAsync();

        channels.ShouldBe(["company-b-channel"]);
    }

    /// <summary>
    /// Verifies that company-owned queries return no rows when no ambient company is available.
    /// </summary>
    [Test]
    public async Task CompanyQueryFilter_WhenCompanyContextMissing_ReturnsNoCompanyOwnedRows()
    {
        var companyA = Guid.CreateVersion7();
        var companyB = Guid.CreateVersion7();
        var companyContext = new CompanyContextAccessor();
        await using var dbContext = CEOAgentDbContextTestFactory.CreateInMemory(companyContext);
        await CompanySeed.SeedChannelsAsync(dbContext, companyA, companyB);

        companyContext.Clear();

        var channels = await dbContext.CompanyChannels
            .WithDefaultTracking()
            .ToListAsync();

        channels.ShouldBeEmpty();
    }
}
