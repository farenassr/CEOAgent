using CeoAgent.Infrastructure.Entities.Filters.Abstractions;

namespace CeoAgent.Infrastructure.Persistence.Extensions;

public static class CompanyOwnedQueryExtensions
{
    public static IQueryable<TEntity> ForCompany<TEntity>(
        this IQueryable<TEntity> query,
        Guid companyId)
        where TEntity : ICompanyOwned
    {
        return query.Where(entity => entity.CompanyId == companyId);
    }
}
