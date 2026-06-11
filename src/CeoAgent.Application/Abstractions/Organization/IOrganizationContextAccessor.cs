namespace CeoAgent.Application.Abstractions.Organization;

public interface IOrganizationContextAccessor : IOrganizationContextProvider
{
    void SetOrganization(Guid organizationId);

    void Clear();
}
