namespace CeoAgent.Infrastructure.Entities.Filters;

/// <summary>
/// Defines a contract for entities that are owned by a specific company.
/// </summary>
public interface ICompanyOwned
{
    /// <summary>
    /// The unique identifier of the company that owns this entity.
    /// </summary>
    Guid CompanyId { get; set; }
}
