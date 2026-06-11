namespace CeoAgent.Infrastructure.Entities.Filters.Abstractions;

/// <summary>
/// Defines a contract for entities that are owned by a specific Keycloak organization.
/// </summary>
public interface IOrganizationOwned
{
    /// <summary>
    /// The unique identifier of the Keycloak organization that owns this entity.
    /// </summary>
    Guid OrganizationId { get; set; }
}
