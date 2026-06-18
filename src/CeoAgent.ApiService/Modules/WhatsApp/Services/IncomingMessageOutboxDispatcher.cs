using CeoAgent.Infrastructure;
using CeoAgent.Infrastructure.Entities;
using CeoAgent.Shared.Enums;
using CeoAgent.Shared.Jobs;
using Microsoft.EntityFrameworkCore;

namespace CeoAgent.ApiService.Modules.WhatsApp;

public sealed partial class IncomingMessageOutboxDispatcher(
    CeoAgentDbContext dbContext,
    IIncomingMessageJobEnqueuer incomingMessageJobEnqueuer,
    TimeProvider timeProvider,
    ILogger<IncomingMessageOutboxDispatcher> logger)
{
    private static readonly TimeSpan ClaimLeaseDuration = TimeSpan.FromMinutes(5);
    private const int MaxFailureReasonLength = 240;

    public async Task<bool> DispatchAsync(Guid outboxId, CancellationToken cancellationToken)
    {
        var outbox = await TryClaimAsync(
            query => query.Where(entity => entity.Id == outboxId),
            cancellationToken);

        return outbox is not null
            && await DispatchAsync(outbox, cancellationToken);
    }

    public async Task<int> DispatchPendingAsync(int maxMessages, CancellationToken cancellationToken)
    {
        if (maxMessages <= 0)
        {
            return 0;
        }

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var staleClaimCutoff = now - ClaimLeaseDuration;
        var candidates = await dbContext.IncomingMessageOutbox
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(entity =>
                ((entity.Status == IncomingMessageOutboxStatus.WaitingToBeQueued
                        || entity.Status == IncomingMessageOutboxStatus.QueueDispatchRetryScheduled)
                    && (entity.NextAttemptAt == null || entity.NextAttemptAt <= now))
                || (entity.Status == IncomingMessageOutboxStatus.QueueDispatchInProgress
                    && entity.ClaimedAt != null
                    && entity.ClaimedAt <= staleClaimCutoff))
            .OrderBy(entity => entity.CreatedAt)
            .Select(entity => entity.Id)
            .Take(maxMessages)
            .ToListAsync(cancellationToken);

        var dispatched = 0;
        foreach (var outboxId in candidates)
        {
            if (await DispatchAsync(outboxId, cancellationToken))
            {
                dispatched++;
            }
        }

        return dispatched;
    }

    private async Task<bool> DispatchAsync(IncomingMessageOutbox outbox, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;

        var job = new ProcessIncomingMessageJob(
            outbox.OrganizationId,
            outbox.ConversationId,
            outbox.MessageId,
            outbox.CorrelationId);

        try
        {
            await incomingMessageJobEnqueuer.EnqueueAsync(job, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            outbox.Status = outbox.AttemptCount >= outbox.MaxAttempts
                ? IncomingMessageOutboxStatus.QueueDispatchFailed
                : IncomingMessageOutboxStatus.QueueDispatchRetryScheduled;
            outbox.NextAttemptAt = outbox.Status == IncomingMessageOutboxStatus.QueueDispatchRetryScheduled
                ? now
                : null;
            outbox.FailureReason = BoundFailureReason(exception.Message);
            await dbContext.SaveChangesAsync(cancellationToken);

            IncomingMessageOutboxDispatchFailed(
                logger,
                exception,
                outbox.OrganizationId,
                outbox.ConversationId,
                outbox.MessageId,
                outbox.Id,
                outbox.AttemptCount);

            return false;
        }

        outbox.Status = IncomingMessageOutboxStatus.QueuedForWorkerProcessing;
        outbox.DispatchedAt = now;
        outbox.NextAttemptAt = null;
        outbox.FailureReason = null;
        await dbContext.SaveChangesAsync(cancellationToken);

        IncomingMessageOutboxDispatchSucceeded(
            logger,
            outbox.OrganizationId,
            outbox.ConversationId,
            outbox.MessageId,
            outbox.Id,
            outbox.AttemptCount);

        return true;
    }

    private async Task<IncomingMessageOutbox?> TryClaimAsync(
        Func<IQueryable<IncomingMessageOutbox>, IQueryable<IncomingMessageOutbox>> filter,
        CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var staleClaimCutoff = now - ClaimLeaseDuration;
        var claimOwner = $"{Environment.MachineName}:{Guid.CreateVersion7():N}";
        var query = filter(dbContext.IncomingMessageOutbox.IgnoreQueryFilters())
            .Where(entity =>
                ((entity.Status == IncomingMessageOutboxStatus.WaitingToBeQueued
                        || entity.Status == IncomingMessageOutboxStatus.QueueDispatchRetryScheduled)
                    && (entity.NextAttemptAt == null || entity.NextAttemptAt <= now))
                || (entity.Status == IncomingMessageOutboxStatus.QueueDispatchInProgress
                    && entity.ClaimedAt != null
                    && entity.ClaimedAt <= staleClaimCutoff));

        var claimed = await query.ExecuteUpdateAsync(
            setters => setters
                .SetProperty(entity => entity.Status, IncomingMessageOutboxStatus.QueueDispatchInProgress)
                .SetProperty(entity => entity.AttemptCount, entity => entity.AttemptCount + 1)
                .SetProperty(entity => entity.LastAttemptAt, now)
                .SetProperty(entity => entity.ClaimedAt, now)
                .SetProperty(entity => entity.ClaimedBy, claimOwner)
                .SetProperty(entity => entity.FailureReason, (string?)null),
            cancellationToken);
        if (claimed == 0)
        {
            return null;
        }

        var claimedOutbox = await dbContext.IncomingMessageOutbox
            .IgnoreQueryFilters()
            .AsNoTracking()
            .SingleAsync(entity => entity.ClaimedBy == claimOwner, cancellationToken);

        var trackedEntry = dbContext.ChangeTracker
            .Entries<IncomingMessageOutbox>()
            .SingleOrDefault(entry =>
                entry.State != EntityState.Deleted
                && entry.Entity.Id == claimedOutbox.Id);
        if (trackedEntry is not null)
        {
            trackedEntry.CurrentValues.SetValues(claimedOutbox);
            return trackedEntry.Entity;
        }

        dbContext.IncomingMessageOutbox.Attach(claimedOutbox);
        return claimedOutbox;
    }

    private static string BoundFailureReason(string message)
    {
        return message.Length <= MaxFailureReasonLength ? message : message[..MaxFailureReasonLength];
    }

}
