using CeoAgent.Application.Abstractions.Organization;
using CeoAgent.Infrastructure.Implementation.Organization;
using Shouldly;

namespace CeoAgent.IntegrationTests.Persistence;

public sealed class OrganizationContextAccessorTests
{
    [Test]
    public void OrganizationId_WhenSetOnOneAccessor_DoesNotLeakToAnotherAccessor()
    {
        var organizationId = Guid.CreateVersion7();
        var firstAccessor = new OrganizationContextAccessor();
        var secondAccessor = new OrganizationContextAccessor();

        firstAccessor.SetOrganization(organizationId);

        firstAccessor.OrganizationId.ShouldBe(organizationId);
        secondAccessor.OrganizationId.ShouldBeNull();
    }
}
