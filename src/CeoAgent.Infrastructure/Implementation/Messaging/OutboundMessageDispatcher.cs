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
    private const int MaxRequestHashLength = 96;

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

        var existingOutbox = await dbContext.OutgoingMessageOutbox
            .IgnoreQueryFilters()
            .SingleOrDefaultAsync(
                entity => entity.OrganizationId == organizationId
                    && entity.IdempotencyKey == idempotencyKey,
                cancellationToken);
        if (existingOutbox?.Status == OutgoingMessageOutboxStatus.SentToProvider
            && !string.IsNullOrWhiteSpace(existingOutbox.ProviderMessageId))
        {
            MarkMessageSent(message, new SentMessageReference(existingOutbox.ProviderMessageId));
            await dbContext.SaveChangesAsync(cancellationToken);
            return new OutboundMessageDispatchResult(existingOutbox.ProviderMessageId, WasAlreadySent: true);
        }

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var outbox = existingOutbox ?? new OutgoingMessageOutbox
        {
            OrganizationId = organizationId,
            ConversationId = conversationId,
            MessageId = messageId,
            Provider = WhatsAppProvider,
            Status = OutgoingMessageOutboxStatus.SendingToProvider,
            IdempotencyKey = idempotencyKey,
            CorrelationId = correlationId,
            ClaimedAt = now,
            ClaimedBy = ClaimOwner,
            AttemptCount = 0,
        };

        outbox.Status = OutgoingMessageOutboxStatus.SendingToProvider;
        outbox.ConversationId = conversationId;
        outbox.MessageId = messageId;
        outbox.AttemptCount++;
        outbox.ClaimedAt = now;
        outbox.ClaimedBy = ClaimOwner;
        outbox.CorrelationId ??= correlationId;
        outbox.NextAttemptAt = null;
        outbox.LastError = null;

        if (existingOutbox is null)
        {
            dbContext.OutgoingMessageOutbox.Add(outbox);
        }

        var ledger = new ProviderSendLedger
        {
            OrganizationId = organizationId,
            OutgoingMessageOutboxId = outbox.Id,
            AttemptNumber = outbox.AttemptCount,
            Provider = WhatsAppProvider,
            Status = ProviderSendLedgerStatus.SendAttemptStarted,
            RequestHash = Bound(idempotencyKey, MaxRequestHashLength),
            StartedAt = now,
            CorrelationId = correlationId,
        };
        dbContext.ProviderSendLedger.Add(ledger);
        await dbContext.SaveChangesAsync(cancellationToken);

        try
        {
            var sent = await sendProviderAsync();
            MarkMessageSent(message, sent);
            var completedAt = timeProvider.GetUtcNow().UtcDateTime;
            outbox.Status = OutgoingMessageOutboxStatus.SentToProvider;
            outbox.ProviderMessageId = sent.ProviderMessageId;
            outbox.SentAt = completedAt;
            outbox.CompletedAt = completedAt;
            outbox.LastError = null;
            ledger.Status = ProviderSendLedgerStatus.ProviderAccepted;
            ledger.ProviderMessageId = sent.ProviderMessageId;
            ledger.FinishedAt = completedAt;
            await dbContext.SaveChangesAsync(cancellationToken);
            return new OutboundMessageDispatchResult(sent.ProviderMessageId, WasAlreadySent: false);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            var failedAt = timeProvider.GetUtcNow().UtcDateTime;
            outbox.Status = outbox.AttemptCount >= outbox.MaxAttempts
                ? OutgoingMessageOutboxStatus.ProviderSendFailed
                : OutgoingMessageOutboxStatus.ProviderSendRetryScheduled;
            outbox.NextAttemptAt = outbox.Status == OutgoingMessageOutboxStatus.ProviderSendRetryScheduled
                ? failedAt
                : null;
            outbox.LastError = Bound(exception.Message, MaxProviderErrorLength);
            ledger.Status = ProviderSendLedgerStatus.ProviderUnavailable;
            ledger.ErrorCode = exception.GetType().Name;
            ledger.ErrorMessage = Bound(exception.Message, MaxProviderErrorLength);
            ledger.FinishedAt = failedAt;
            await dbContext.SaveChangesAsync(cancellationToken);
            OutboundProviderSendFailed(logger, exception, organizationId, conversationId, messageId, outbox.Id, outbox.AttemptCount);
            throw;
        }
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
        Message = "OutboundProviderSendFailed OrganizationId={OrganizationId} ConversationId={ConversationId} MessageId={MessageId} OutboxId={OutboxId} AttemptCount={AttemptCount}")]
    private static partial void OutboundProviderSendFailed(
        ILogger logger,
        Exception exception,
        Guid organizationId,
        Guid conversationId,
        Guid messageId,
        Guid outboxId,
        int attemptCount);
}
