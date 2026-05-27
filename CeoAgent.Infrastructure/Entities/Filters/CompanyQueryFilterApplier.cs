using Microsoft.EntityFrameworkCore;
using System.Reflection;

namespace CeoAgent.Infrastructure.Entities.Filters;

public static class CompanyQueryFilterApplier
{
    public static void ApplyCompanyFilters(ModelBuilder modelBuilder, CeoAgentDbContext dbContext)
    {
        var method = typeof(CompanyQueryFilterApplier)
            .GetMethod(nameof(ConfigureCompanyOwnedFilter), BindingFlags.NonPublic | BindingFlags.Static);

        if (method == null)
        {
            throw new InvalidOperationException($"Could not find method {nameof(ConfigureCompanyOwnedFilter)} on {nameof(CompanyQueryFilterApplier)}.");
        }

        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (entityType.ClrType != null && typeof(ICompanyOwned).IsAssignableFrom(entityType.ClrType))
            {
                var genericMethod = method.MakeGenericMethod(entityType.ClrType);
                genericMethod.Invoke(null, [modelBuilder, dbContext]);
            }
        }
    }

    private static void ConfigureCompanyOwnedFilter<TEntity>(ModelBuilder modelBuilder, CeoAgentDbContext dbContext)
        where TEntity : class, ICompanyOwned
    {
        modelBuilder.Entity<TEntity>().HasQueryFilter(entity => dbContext.CurrentCompanyId.HasValue && entity.CompanyId == dbContext.CurrentCompanyId);
    }
}
