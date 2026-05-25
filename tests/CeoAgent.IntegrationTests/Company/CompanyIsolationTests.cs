using CeoAgent.Application.Company;
using CeoAgent.Infrastructure.Persistence;
using CeoAgent.IntegrationTests.Infrastructure;
using CeoAgent.IntegrationTests.Seed;
using Microsoft.EntityFrameworkCore;
using Shouldly;

namespace CeoAgent.IntegrationTests.Company;

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
        await using var dbContext = CeoAgentDbContextTestFactory.CreateInMemory(companyContext);
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
        await using var dbContext = CeoAgentDbContextTestFactory.CreateInMemory(companyContext);
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
        await using var dbContext = CeoAgentDbContextTestFactory.CreateInMemory(companyContext);
        await CompanySeed.SeedChannelsAsync(dbContext, companyA, companyB);

        companyContext.Clear();

        var channels = await dbContext.CompanyChannels
            .WithDefaultTracking()
            .Select(entity => entity.Id)
            .ToListAsync();

        channels.ShouldBeEmpty();
    }

    /// <summary>
    /// Verifies that company-owned rows must carry an explicit non-empty company ID.
    /// </summary>
    [Test]
    public async Task SaveChanges_WhenCompanyOwnedEntityHasEmptyCompanyId_Throws()
    {
        var companyContext = new CompanyContextAccessor();
        companyContext.SetCompany(Guid.CreateVersion7());
        await using var dbContext = CeoAgentDbContextTestFactory.CreateInMemory(companyContext);

        dbContext.Customers.Add(new CeoAgent.Infrastructure.Entities.Customer
        {
            CompanyChannelId = Guid.CreateVersion7(),
            ExternalCustomerId = "573001112233"
        });

        var exception = Should.Throw<InvalidOperationException>(() => dbContext.SaveChangesAsync());
        exception.Message.ShouldContain("requires a non-empty CompanyId");
    }
}
