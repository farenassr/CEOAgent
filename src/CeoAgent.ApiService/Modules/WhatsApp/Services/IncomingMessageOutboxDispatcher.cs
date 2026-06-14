using CeoAgent.Infrastructure;
using CeoAgent.Infrastructure.Entities;
using CeoAgent.Shared.Jobs;
using Microsoft.EntityFrameworkCore;

namespace CeoAgent.ApiService.Modules.WhatsApp;

public sealed partial class IncomingMessageOutboxDispatcher(
    CeoAgentDbContext dbContext,
    IIncomingMessageJobEnqueuer incomingMessageJobEnqueuer,
    TimeProvider timeProvider,
    ILogger<IncomingMessageOutboxDispatcher> logger)
{
    public async Task<bool> DispatchAsync(Guid outboxId, CancellationToken cancellationToken)
    {
        var outbox = await dbContext.IncomingMessageOutbox
            .IgnoreQueryFilters()
            .SingleOrDefaultAsync(entity => entity.Id == outboxId, cancellationToken);

        return outbox is not null
            && await DispatchAsync(outbox, cancellationToken);
    }

    public async Task<int> DispatchPendingAsync(int maxMessages, CancellationToken cancellationToken)
    {
        if (maxMessages <= 0)
        {
            return 0;
        }

        var pending = await dbContext.IncomingMessageOutbox
            .IgnoreQueryFilters()
            .Where(entity => entity.Status != IncomingMessageOutboxStatus.Dispatched)
            .OrderBy(entity => entity.CreatedAt)
            .Take(maxMessages)
            .ToListAsync(cancellationToken);

        var dispatched = 0;
        foreach (var outbox in pending)
        {
            if (await DispatchAsync(outbox, cancellationToken))
            {
                dispatched++;
            }
        }

        return dispatched;
    }

    private async Task<bool> DispatchAsync(IncomingMessageOutbox outbox, CancellationToken cancellationToken)
    {
        if (outbox.Status == IncomingMessageOutboxStatus.Dispatched)
        {
            return false;
        }

        var now = timeProvider.GetUtcNow().UtcDateTime;
        outbox.AttemptCount++;
        outbox.LastAttemptAt = now;

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
            outbox.Status = IncomingMessageOutboxStatus.Failed;
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

        outbox.Status = IncomingMessageOutboxStatus.Dispatched;
        outbox.DispatchedAt = now;
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

    private static string BoundFailureReason(string message)
    {
        return message.Length <= 240 ? message : message[..240];
    }

}
