using CeoAgent.Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;

namespace CeoAgent.Infrastructure.Persistence.Extensions;

public static class ToolExecutionQueryExtensions
{
    public static IQueryable<ToolExecution> WithIdempotencyKey(
        this IQueryable<ToolExecution> query,
        Guid organizationId,
        string idempotencyKey)
    {
        return query
            .ForOrganization(organizationId)
            .Where(entity => entity.IdempotencyKey == idempotencyKey);
    }

    public static async Task<ToolExecution?> FindTrackedOrPersistedToolExecutionAsync(
        this CeoAgentDbContext dbContext,
        Guid organizationId,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        var tracked = dbContext.ChangeTracker
            .Entries<ToolExecution>()
            .Where(entry => entry.State != EntityState.Deleted)
            .Select(entry => entry.Entity)
            .SingleOrDefault(entity =>
                entity.OrganizationId == organizationId
                && entity.IdempotencyKey == idempotencyKey);

        if (tracked is not null)
        {
            return tracked;
        }

        return await dbContext.ToolExecutions
            .WithIdempotencyKey(organizationId, idempotencyKey)
            .SingleOrDefaultAsync(cancellationToken);
    }
}
