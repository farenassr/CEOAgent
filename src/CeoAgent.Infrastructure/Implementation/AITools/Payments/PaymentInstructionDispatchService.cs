using CeoAgent.Application.Abstractions.Messaging;
using CeoAgent.Application.Abstractions.Payments;
using CeoAgent.Infrastructure.Entities;
using CeoAgent.Infrastructure.Entities.JsonDocuments;
using CeoAgent.Infrastructure.Persistence.Extensions;
using CeoAgent.Shared.Enums;
using CeoAgent.Shared.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CeoAgent.Infrastructure.Implementation.AITools.Payments;

public sealed partial class PaymentInstructionDispatchService(
    CeoAgentDbContext dbContext,
    IOutboundMessageDispatcher outboundMessageDispatcher,
    IPaymentQrImageProvider qrImageProvider,
    TimeProvider timeProvider,
    ILogger<PaymentInstructionDispatchService> logger)
{
    private const string PaymentQrSendFailedFailureReason = "payment_qr_send_failed";

    public async Task<PaymentInstructionDispatchOutcome> SendPaymentImageAsync(
        ToolExecution reservationExecution,
        CompanyPaymentAccount account,
        Guid companyChannelId,
        string recipientExternalId,
        string caption,
        CancellationToken cancellationToken)
    {
        var paymentIdempotencyKey = $"payment:{reservationExecution.Id}";
        var existing = await dbContext.Messages
            .ForConversation(reservationExecution.OrganizationId, reservationExecution.ConversationId)
            .SingleOrDefaultAsync(
                message => message.ProviderMessageId == paymentIdempotencyKey,
                cancellationToken);

        if (existing?.Payload?.ProviderMessageId is { Length: > 0 })
        {
            return PaymentInstructionDispatchOutcome.AlreadySent(existing.Id);
        }

        var paymentMessage = existing ?? new Message
        {
            OrganizationId = reservationExecution.OrganizationId,
            ConversationId = reservationExecution.ConversationId,
            Role = MessageRole.Assistant,
            ProviderMessageId = paymentIdempotencyKey,
            OccurredAt = timeProvider.GetUtcNow().UtcDateTime,
        };

        paymentMessage.Type = MessageType.Image;
        paymentMessage.MessageText = caption;
        paymentMessage.Payload = new MessagePayload
        {
            ProviderType = "image",
            BlobContainer = account.QrBlobContainer,
            BlobName = account.QrBlobName,
            BlobUri = account.QrBlobUri,
        };

        if (existing is null)
        {
            dbContext.Messages.Add(paymentMessage);
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        try
        {
            var qr = await qrImageProvider.GetQrImageAsync(account.QrBlobContainer, account.QrBlobName, cancellationToken);
            _ = await outboundMessageDispatcher.SendImageAsync(
                new OutboundImageDispatchRequest(
                    reservationExecution.OrganizationId,
                    companyChannelId,
                    reservationExecution.ConversationId,
                    paymentMessage.Id,
                    recipientExternalId,
                    qr.Content,
                    qr.ContentType,
                    qr.FileName,
                    caption,
                    paymentIdempotencyKey),
                cancellationToken);

            return PaymentInstructionDispatchOutcome.Sent(paymentMessage.Id);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            PaymentQrImageSendFailed(
                logger,
                exception,
                reservationExecution.OrganizationId,
                reservationExecution.ConversationId,
                reservationExecution.Id);
            return PaymentInstructionDispatchOutcome.Failed(PaymentQrSendFailedFailureReason);
        }
    }

    public async Task<PaymentInstructionFallbackOutcome> SendFallbackTextAsync(
        Guid organizationId,
        Guid conversationId,
        Guid companyChannelId,
        string recipientExternalId,
        string text,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        var message = await dbContext.Messages
            .ForConversation(organizationId, conversationId)
            .SingleOrDefaultAsync(entity => entity.ProviderMessageId == idempotencyKey, cancellationToken);

        if (message?.Payload?.ProviderMessageId is { Length: > 0 })
        {
            return new PaymentInstructionFallbackOutcome(message.Id, CustomerVisibleMessageSent: true);
        }

        message ??= new Message
        {
            OrganizationId = organizationId,
            ConversationId = conversationId,
            Role = MessageRole.Assistant,
            ProviderMessageId = idempotencyKey,
            OccurredAt = timeProvider.GetUtcNow().UtcDateTime,
        };

        message.Type = MessageType.Text;
        message.MessageText = text;
        message.Payload = new MessagePayload { ProviderType = "text" };

        if (dbContext.Entry(message).State == EntityState.Detached)
        {
            dbContext.Messages.Add(message);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        _ = await outboundMessageDispatcher.SendTextAsync(
            new OutboundTextDispatchRequest(
                organizationId,
                companyChannelId,
                conversationId,
                message.Id,
                recipientExternalId,
                text,
                idempotencyKey),
            cancellationToken);

        return new PaymentInstructionFallbackOutcome(message.Id, CustomerVisibleMessageSent: true);
    }
}

public sealed record PaymentInstructionDispatchOutcome(
    bool PaymentInstructionsSent,
    bool CustomerVisibleMessageSent,
    Guid? PaymentMessageId,
    ToolExecutionStatus Status,
    string? FailureReason)
{
    public static PaymentInstructionDispatchOutcome Sent(Guid paymentMessageId)
    {
        return new PaymentInstructionDispatchOutcome(
            PaymentInstructionsSent: true,
            CustomerVisibleMessageSent: true,
            PaymentMessageId: paymentMessageId,
            ToolExecutionStatus.ToolExecutionSucceeded,
            FailureReason: null);
    }

    public static PaymentInstructionDispatchOutcome AlreadySent(Guid paymentMessageId)
    {
        return Sent(paymentMessageId);
    }

    public static PaymentInstructionDispatchOutcome Failed(string failureReason)
    {
        return new PaymentInstructionDispatchOutcome(
            PaymentInstructionsSent: false,
            CustomerVisibleMessageSent: false,
            PaymentMessageId: null,
            ToolExecutionStatus.ToolExecutionFailed,
            failureReason);
    }
}

public sealed record PaymentInstructionFallbackOutcome(
    Guid MessageId,
    bool CustomerVisibleMessageSent);
