namespace CeoAgent.Application.Abstractions.Organization;

public interface IOrganizationContextProvider
{
    Guid? OrganizationId { get; }
}
