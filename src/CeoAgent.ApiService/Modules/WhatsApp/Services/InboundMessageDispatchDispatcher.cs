using CeoAgent.Infrastructure;
using CeoAgent.Infrastructure.Entities;
using CeoAgent.Infrastructure.Persistence.Extensions;
using CeoAgent.Shared.Enums;
using CeoAgent.Shared.Jobs;
using Microsoft.EntityFrameworkCore;

namespace CeoAgent.ApiService.Modules.WhatsApp;

public sealed partial class InboundMessageDispatchDispatcher(
    CeoAgentDbContext dbContext,
    IIncomingMessageJobEnqueuer incomingMessageJobEnqueuer,
    TimeProvider timeProvider,
    ILogger<InboundMessageDispatchDispatcher> logger)
{
    private static readonly TimeSpan ClaimLeaseDuration = TimeSpan.FromMinutes(5);
    private const int MaxLastErrorLength = 500;

    public async Task<bool> DispatchAsync(Guid dispatchId, CancellationToken cancellationToken)
    {
        var dispatch = await TryClaimAsync(
            query => query.Where(entity => entity.Id == dispatchId),
            cancellationToken);

        return dispatch is not null
            && await DispatchAsync(dispatch, cancellationToken);
    }

    public async Task<int> DispatchPendingAsync(int maxMessages, CancellationToken cancellationToken)
    {
        if (maxMessages <= 0)
        {
            return 0;
        }

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var candidates = await dbContext.MessageDispatches
            .IgnoreQueryFilters()
            .AsNoTracking()
            .DispatchableAt(MessageDispatchOperation.InboundQueueDispatch, now, ClaimLeaseDuration)
            .OrderBy(entity => entity.CreatedAt)
            .Select(entity => entity.Id)
            .Take(maxMessages)
            .ToListAsync(cancellationToken);

        var dispatched = 0;
        foreach (var dispatchId in candidates)
        {
            if (await DispatchAsync(dispatchId, cancellationToken))
            {
                dispatched++;
            }
        }

        return dispatched;
    }

    private async Task<bool> DispatchAsync(MessageDispatch dispatch, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var job = new ProcessIncomingMessageJob(
            dispatch.OrganizationId,
            dispatch.ConversationId,
            dispatch.MessageId,
            dispatch.CorrelationId);

        try
        {
            await incomingMessageJobEnqueuer.EnqueueAsync(job, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            dispatch.Status = dispatch.AttemptCount >= dispatch.MaxAttempts
                ? MessageDispatchStatus.Failed
                : MessageDispatchStatus.RetryScheduled;
            dispatch.NextAttemptAt = dispatch.Status == MessageDispatchStatus.RetryScheduled
                ? now
                : null;
            dispatch.LastError = Bound(exception.Message, MaxLastErrorLength);
            await dbContext.SaveChangesAsync(cancellationToken);

            InboundMessageDispatchFailed(
                logger,
                exception,
                dispatch.OrganizationId,
                dispatch.ConversationId,
                dispatch.MessageId,
                dispatch.Id,
                dispatch.AttemptCount);

            return false;
        }

        dispatch.Status = MessageDispatchStatus.Succeeded;
        dispatch.SucceededAt = now;
        dispatch.NextAttemptAt = null;
        dispatch.LastError = null;
        await dbContext.SaveChangesAsync(cancellationToken);

        InboundMessageDispatchSucceeded(
            logger,
            dispatch.OrganizationId,
            dispatch.ConversationId,
            dispatch.MessageId,
            dispatch.Id,
            dispatch.AttemptCount);

        return true;
    }

    private async Task<MessageDispatch?> TryClaimAsync(
        Func<IQueryable<MessageDispatch>, IQueryable<MessageDispatch>> filter,
        CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var claimOwner = $"{Environment.MachineName}:{Guid.CreateVersion7():N}";
        var query = filter(dbContext.MessageDispatches.IgnoreQueryFilters())
            .DispatchableAt(MessageDispatchOperation.InboundQueueDispatch, now, ClaimLeaseDuration);

        var claimed = await query.ExecuteUpdateAsync(
            setters => setters
                .SetProperty(entity => entity.Status, MessageDispatchStatus.InProgress)
                .SetProperty(entity => entity.AttemptCount, entity => entity.AttemptCount + 1)
                .SetProperty(entity => entity.LastAttemptAt, now)
                .SetProperty(entity => entity.ClaimedAt, now)
                .SetProperty(entity => entity.ClaimedBy, claimOwner)
                .SetProperty(entity => entity.LastError, (string?)null),
            cancellationToken);
        if (claimed == 0)
        {
            return null;
        }

        var claimedDispatch = await dbContext.MessageDispatches
            .IgnoreQueryFilters()
            .AsNoTracking()
            .SingleAsync(entity => entity.ClaimedBy == claimOwner, cancellationToken);

        var trackedEntry = dbContext.ChangeTracker
            .Entries<MessageDispatch>()
            .SingleOrDefault(entry =>
                entry.State != EntityState.Deleted
                && entry.Entity.Id == claimedDispatch.Id);
        if (trackedEntry is not null)
        {
            trackedEntry.CurrentValues.SetValues(claimedDispatch);
            return trackedEntry.Entity;
        }

        dbContext.MessageDispatches.Attach(claimedDispatch);
        return claimedDispatch;
    }

    private static string Bound(string value, int length)
    {
        return value.Length <= length ? value : value[..length];
    }
}
