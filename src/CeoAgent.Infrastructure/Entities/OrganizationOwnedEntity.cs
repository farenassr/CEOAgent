using CeoAgent.Infrastructure.Entities.Filters.Abstractions;

namespace CeoAgent.Infrastructure.Entities;

public abstract class OrganizationOwnedEntity : IOrganizationOwned
{
    /// <summary>
    /// Keycloak organization that owns this row. Example: b36cfb51-83bd-4376-b7d7-0502141ff6ae.
    /// </summary>
    public Guid OrganizationId { get; set; }
}
