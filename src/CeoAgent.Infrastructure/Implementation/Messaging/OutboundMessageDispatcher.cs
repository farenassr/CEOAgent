using CeoAgent.Application.Abstractions.Messaging;
using CeoAgent.Infrastructure.Entities;
using CeoAgent.Infrastructure.Entities.JsonDocuments;
using CeoAgent.Infrastructure.Persistence.Extensions;
using CeoAgent.Shared.Enums;
using CeoAgent.Shared.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CeoAgent.Infrastructure.Implementation.Messaging;

public sealed partial class OutboundMessageDispatcher(
    CeoAgentDbContext dbContext,
    IMessageChannelIntegration messaging,
    TimeProvider timeProvider,
    ILogger<OutboundMessageDispatcher> logger) : IOutboundMessageDispatcher
{
    private const string WhatsAppProvider = "whatsapp_cloud";
    private const string ClaimOwner = "outbound-dispatcher";
    private const int MaxProviderErrorLength = 500;
    private static readonly TimeSpan ClaimLeaseDuration = TimeSpan.FromMinutes(5);

    public Task<OutboundMessageDispatchResult> SendTextAsync(
        OutboundTextDispatchRequest request,
        CancellationToken cancellationToken)
    {
        return DispatchAsync(
            request.OrganizationId,
            request.ConversationId,
            request.MessageId,
            request.IdempotencyKey,
            request.CorrelationId,
            () => messaging.SendTextAsync(
                new ChannelTextMessage(
                    request.OrganizationId,
                    request.CompanyChannelId,
                    request.ConversationId,
                    request.MessageId,
                    request.RecipientExternalId,
                    request.Text,
                    request.IdempotencyKey),
                cancellationToken),
            cancellationToken);
    }

    public Task<OutboundMessageDispatchResult> SendImageAsync(
        OutboundImageDispatchRequest request,
        CancellationToken cancellationToken)
    {
        return DispatchAsync(
            request.OrganizationId,
            request.ConversationId,
            request.MessageId,
            request.IdempotencyKey,
            request.CorrelationId,
            () => messaging.SendImageAsync(
                new ChannelImageMessage(
                    request.OrganizationId,
                    request.CompanyChannelId,
                    request.ConversationId,
                    request.MessageId,
                    request.RecipientExternalId,
                    request.Content,
                    request.ContentType,
                    request.FileName,
                    request.Caption,
                    request.IdempotencyKey),
                cancellationToken),
            cancellationToken);
    }

    private async Task<OutboundMessageDispatchResult> DispatchAsync(
        Guid organizationId,
        Guid conversationId,
        Guid messageId,
        string idempotencyKey,
        string? correlationId,
        Func<Task<SentMessageReference>> sendProviderAsync,
        CancellationToken cancellationToken)
    {
        var message = await dbContext.FindTrackedOrPersistedMessageAsync(organizationId, messageId, cancellationToken)
            ?? throw new InvalidOperationException($"Outbound message '{messageId}' was not found.");

        var existingDispatch = await dbContext.MessageDispatches
            .IgnoreQueryFilters()
            .SingleOrDefaultAsync(
                entity => entity.OrganizationId == organizationId
                    && entity.Operation == MessageDispatchOperation.OutboundProviderSend
                    && entity.IdempotencyKey == idempotencyKey,
                cancellationToken);
        if (existingDispatch?.Status == MessageDispatchStatus.Succeeded
            && !string.IsNullOrWhiteSpace(existingDispatch.ProviderMessageId))
        {
            MarkMessageSent(message, new SentMessageReference(existingDispatch.ProviderMessageId));
            await dbContext.SaveChangesAsync(cancellationToken);
            return new OutboundMessageDispatchResult(existingDispatch.ProviderMessageId, WasAlreadySent: true);
        }

        var dispatch = existingDispatch ?? new MessageDispatch
        {
            OrganizationId = organizationId,
            ConversationId = conversationId,
            MessageId = messageId,
            Operation = MessageDispatchOperation.OutboundProviderSend,
            Provider = WhatsAppProvider,
            Status = MessageDispatchStatus.Pending,
            IdempotencyKey = idempotencyKey,
            CorrelationId = correlationId,
            AttemptCount = 0,
        };

        if (existingDispatch is null)
        {
            dbContext.MessageDispatches.Add(dispatch);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        dispatch = await ClaimDispatchAsync(dispatch.Id, conversationId, messageId, correlationId, cancellationToken)
            ?? throw new InvalidOperationException($"Outbound message dispatch '{dispatch.Id}' is not dispatchable.");

        try
        {
            var sent = await sendProviderAsync();
            MarkMessageSent(message, sent);
            var completedAt = timeProvider.GetUtcNow().UtcDateTime;
            dispatch.Status = MessageDispatchStatus.Succeeded;
            dispatch.ProviderMessageId = sent.ProviderMessageId;
            dispatch.SucceededAt = completedAt;
            dispatch.NextAttemptAt = null;
            dispatch.LastError = null;
            await dbContext.SaveChangesAsync(cancellationToken);
            return new OutboundMessageDispatchResult(sent.ProviderMessageId, WasAlreadySent: false);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            var failedAt = timeProvider.GetUtcNow().UtcDateTime;
            dispatch.Status = dispatch.AttemptCount >= dispatch.MaxAttempts
                ? MessageDispatchStatus.Failed
                : MessageDispatchStatus.RetryScheduled;
            dispatch.NextAttemptAt = dispatch.Status == MessageDispatchStatus.RetryScheduled
                ? failedAt
                : null;
            dispatch.LastError = Bound(exception.Message, MaxProviderErrorLength);
            await dbContext.SaveChangesAsync(cancellationToken);
            OutboundProviderSendFailed(logger, exception, organizationId, conversationId, messageId, dispatch.Id, dispatch.AttemptCount);
            throw;
        }
    }

    private async Task<MessageDispatch?> ClaimDispatchAsync(
        Guid dispatchId,
        Guid conversationId,
        Guid messageId,
        string? correlationId,
        CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var claimOwner = $"{ClaimOwner}:{Environment.MachineName}:{Guid.CreateVersion7():N}";
        var claimed = await dbContext.MessageDispatches
            .IgnoreQueryFilters()
            .Where(entity => entity.Id == dispatchId)
            .DispatchableAt(MessageDispatchOperation.OutboundProviderSend, now, ClaimLeaseDuration)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(entity => entity.Status, MessageDispatchStatus.InProgress)
                    .SetProperty(entity => entity.ConversationId, conversationId)
                    .SetProperty(entity => entity.MessageId, messageId)
                    .SetProperty(entity => entity.Provider, WhatsAppProvider)
                    .SetProperty(entity => entity.AttemptCount, entity => entity.AttemptCount + 1)
                    .SetProperty(entity => entity.LastAttemptAt, now)
                    .SetProperty(entity => entity.ClaimedAt, now)
                    .SetProperty(entity => entity.ClaimedBy, claimOwner)
                    .SetProperty(entity => entity.CorrelationId, entity => entity.CorrelationId ?? correlationId)
                    .SetProperty(entity => entity.NextAttemptAt, (DateTime?)null)
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

    private static void MarkMessageSent(Message message, SentMessageReference sent)
    {
        message.Payload ??= new MessagePayload();
        message.Payload.ProviderMessageId = sent.ProviderMessageId;
    }

    private static string Bound(string value, int length)
    {
        return value.Length <= length ? value : value[..length];
    }

    [LoggerMessage(
        EventId = 3101,
        Level = LogLevel.Warning,
        Message = "OutboundProviderSendFailed OrganizationId={OrganizationId} ConversationId={ConversationId} MessageId={MessageId} DispatchId={DispatchId} AttemptCount={AttemptCount}")]
    private static partial void OutboundProviderSendFailed(
        ILogger logger,
        Exception exception,
        Guid organizationId,
        Guid conversationId,
        Guid messageId,
        Guid dispatchId,
        int attemptCount);
}
