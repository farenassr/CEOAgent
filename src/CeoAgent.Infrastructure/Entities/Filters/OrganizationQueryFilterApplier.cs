using Microsoft.EntityFrameworkCore;
using System.Reflection;
using CeoAgent.Infrastructure.Entities.Filters.Abstractions;

namespace CeoAgent.Infrastructure.Entities.Filters;

public static class OrganizationQueryFilterApplier
{
    public static void ApplyOrganizationFilters(ModelBuilder modelBuilder, CeoAgentDbContext dbContext)
    {
        var method = typeof(OrganizationQueryFilterApplier)
            .GetMethod(nameof(ConfigureOrganizationOwnedFilter), BindingFlags.NonPublic | BindingFlags.Static);

        if (method == null)
        {
            throw new InvalidOperationException($"Could not find method {nameof(ConfigureOrganizationOwnedFilter)} on {nameof(OrganizationQueryFilterApplier)}.");
        }

        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (entityType.ClrType != null && typeof(IOrganizationOwned).IsAssignableFrom(entityType.ClrType))
            {
                var genericMethod = method.MakeGenericMethod(entityType.ClrType);
                genericMethod.Invoke(null, [modelBuilder, dbContext]);
            }
        }
    }

    private static void ConfigureOrganizationOwnedFilter<TEntity>(ModelBuilder modelBuilder, CeoAgentDbContext dbContext)
        where TEntity : class, IOrganizationOwned
    {
        modelBuilder.Entity<TEntity>().HasQueryFilter(entity => dbContext.CurrentOrganizationId.HasValue && entity.OrganizationId == dbContext.CurrentOrganizationId);
    }
}
