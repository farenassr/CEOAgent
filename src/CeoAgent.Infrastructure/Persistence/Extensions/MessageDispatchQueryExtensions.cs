using CeoAgent.Infrastructure.Entities;
using CeoAgent.Shared.Enums;

namespace CeoAgent.Infrastructure.Persistence.Extensions;

public static class MessageDispatchQueryExtensions
{
    public static IQueryable<MessageDispatch> DispatchableAt(
        this IQueryable<MessageDispatch> query,
        MessageDispatchOperation operation,
        DateTime utcNow,
        TimeSpan claimLeaseDuration)
    {
        var staleClaimCutoff = utcNow - claimLeaseDuration;
        return query.Where(entity =>
            entity.Operation == operation
            && (((entity.Status == MessageDispatchStatus.Pending
                    || entity.Status == MessageDispatchStatus.RetryScheduled)
                && (entity.NextAttemptAt == null || entity.NextAttemptAt <= utcNow))
            || (entity.Status == MessageDispatchStatus.InProgress
                && entity.ClaimedAt != null
                && entity.ClaimedAt <= staleClaimCutoff)));
    }
}
