using CeoAgent.Infrastructure.Entities.Filters.Abstractions;

namespace CeoAgent.Infrastructure.Persistence.Extensions;

public static class OrganizationOwnedQueryExtensions
{
    public static IQueryable<TEntity> ForOrganization<TEntity>(
        this IQueryable<TEntity> query,
        Guid organizationId)
        where TEntity : IOrganizationOwned
    {
        return query.Where(entity => entity.OrganizationId == organizationId);
    }
}
