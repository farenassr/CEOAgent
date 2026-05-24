using Microsoft.EntityFrameworkCore;

namespace CEOAgent.Infrastructure.Persistence;

public static class QueryTrackingExtensions
{
    public static IQueryable<TEntity> WithDefaultTracking<TEntity>(
        this IQueryable<TEntity> query,
        bool trackChanges = false) where TEntity : class
    {
        return trackChanges ? query.AsTracking() : query.AsNoTracking();
    }
}
