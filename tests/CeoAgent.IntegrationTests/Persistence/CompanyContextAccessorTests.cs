using CeoAgent.Application.Abstractions.Company;
using CeoAgent.Infrastructure.Implementation.Company;
using Shouldly;

namespace CeoAgent.IntegrationTests.Persistence;

public sealed class CompanyContextAccessorTests
{
    [Test]
    public void CompanyId_WhenSetOnOneAccessor_DoesNotLeakToAnotherAccessor()
    {
        var companyId = Guid.CreateVersion7();
        var firstAccessor = new CompanyContextAccessor();
        var secondAccessor = new CompanyContextAccessor();

        firstAccessor.SetCompany(companyId);

        firstAccessor.CompanyId.ShouldBe(companyId);
        secondAccessor.CompanyId.ShouldBeNull();
    }
}
