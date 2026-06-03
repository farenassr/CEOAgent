using CeoAgent.Application.Company.Abstractions;
using CeoAgent.Application.Company.Implementation;
using Shouldly;

namespace CeoAgent.Application.Tests.Company;

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
