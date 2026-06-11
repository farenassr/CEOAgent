using CeoAgent.Application.Abstractions.Organization;
using CeoAgent.Infrastructure.Implementation.Organization;
using CeoAgent.Infrastructure.Persistence;
using CeoAgent.IntegrationTests.Infrastructure;
using CeoAgent.IntegrationTests.Seed;
using Microsoft.EntityFrameworkCore;
using Shouldly;

namespace CeoAgent.IntegrationTests.Company;

public sealed class CompanyIsolationTests
{
    /// <summary>
    /// Verifies that company query filters only return Organization A rows when Organization A is the ambient company.
    /// </summary>
    [Test]
    public async Task OrganizationQueryFilter_WhenCompanyAContext_DoesNotReturnCompanyBRows()
    {
        var companyA = Guid.CreateVersion7();
        var companyB = Guid.CreateVersion7();
        var companyContext = new OrganizationContextAccessor();
        await using var dbContext = CeoAgentDbContextTestFactory.CreateInMemory(companyContext);
        await CompanySeed.SeedChannelsAsync(dbContext, companyA, companyB);

        companyContext.SetOrganization(companyA);

        var channels = await dbContext.CompanyChannels
            .WithDefaultTracking()
            .Select(entity => entity.ProviderChannelId)
            .ToListAsync();

        channels.ShouldBe(["company-a-channel"]);
    }

    /// <summary>
    /// Verifies that company query filters only return Organization B rows when Organization B is the ambient company.
    /// </summary>
    [Test]
    public async Task OrganizationQueryFilter_WhenCompanyBContext_DoesNotReturnCompanyARows()
    {
        var companyA = Guid.CreateVersion7();
        var companyB = Guid.CreateVersion7();
        var companyContext = new OrganizationContextAccessor();
        await using var dbContext = CeoAgentDbContextTestFactory.CreateInMemory(companyContext);
        await CompanySeed.SeedChannelsAsync(dbContext, companyA, companyB);

        companyContext.SetOrganization(companyB);

        var channels = await dbContext.CompanyChannels
            .WithDefaultTracking()
            .Select(entity => entity.ProviderChannelId)
            .ToListAsync();

        channels.ShouldBe(["company-b-channel"]);
    }

    /// <summary>
    /// Verifies that organization-owned queries return no rows when no ambient company is available.
    /// </summary>
    [Test]
    public async Task OrganizationQueryFilter_WhenOrganizationContextMissing_ReturnsNoOrganizationOwnedRows()
    {
        var companyA = Guid.CreateVersion7();
        var companyB = Guid.CreateVersion7();
        var companyContext = new OrganizationContextAccessor();
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
    /// Verifies that organization-owned rows must carry an explicit non-empty company ID.
    /// </summary>
    [Test]
    public async Task SaveChanges_WhenOrganizationOwnedEntityHasEmptyOrganizationId_Throws()
    {
        var companyContext = new OrganizationContextAccessor();
        companyContext.SetOrganization(Guid.CreateVersion7());
        await using var dbContext = CeoAgentDbContextTestFactory.CreateInMemory(companyContext);

        dbContext.Customers.Add(new CeoAgent.Infrastructure.Entities.Customer
        {
            CompanyChannelId = Guid.CreateVersion7(),
            ExternalCustomerId = "573001112233"
        });

        var exception = Should.Throw<InvalidOperationException>(() => dbContext.SaveChangesAsync());
        exception.Message.ShouldContain("requires a non-empty OrganizationId");
    }
}
