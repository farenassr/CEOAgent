using CeoAgent.Application.Abstractions.Organization;

namespace CeoAgent.Infrastructure.Implementation.Organization;

public sealed class OrganizationContextAccessor : IOrganizationContextAccessor
{
    public Guid? OrganizationId { get; private set; }

    public void SetOrganization(Guid organizationId)
    {
        OrganizationId = organizationId;
    }

    public void Clear()
    {
        OrganizationId = null;
    }
}
