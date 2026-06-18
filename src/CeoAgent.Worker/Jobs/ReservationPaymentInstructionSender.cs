using System.Globalization;
using CeoAgent.Application.Abstractions.Messaging;
using CeoAgent.Application.Abstractions.Payments;
using CeoAgent.Infrastructure;
using CeoAgent.Infrastructure.Entities;
using CeoAgent.Infrastructure.Entities.JsonDocuments;
using CeoAgent.Infrastructure.Implementation.AITools.Handoff;
using CeoAgent.Infrastructure.Persistence.Extensions;
using CeoAgent.Shared.Constants;
using CeoAgent.Shared.Enums;
using CeoAgent.Shared.Messaging;
using Microsoft.EntityFrameworkCore;

namespace CeoAgent.Worker.Jobs;

public sealed partial class ReservationPaymentInstructionSender(
    CeoAgentDbContext dbContext,
    IOutboundMessageDispatcher outboundMessageDispatcher,
    IPaymentQrImageProvider qrImageProvider,
    HumanHandoffToolExecutor handoffExecutor,
    TimeProvider timeProvider,
    ILogger<ReservationPaymentInstructionSender> logger)
{
    private const string AwaitingPaymentConfirmation = "awaiting_reservation_payment_confirmation";
    private const string ReceiptReceived = "reservation_payment_receipt_received";
    private const string PaymentIntent = "reservation_payment";
    private const string PaymentFallbackText = "La reserva quedo creada, pero necesito que una persona del equipo te comparta la informacion de pago.";
    private const string ReceiptConfirmationText = "Recibimos tu comprobante. Una persona del equipo continuara con la confirmacion.";

    public async Task SendForSuccessfulReservationsAsync(
        Guid organizationId,
        Guid conversationId,
        Guid inboundMessageId,
        Guid companyChannelId,
        string recipientExternalId,
        CancellationToken cancellationToken)
    {
        var idempotencyPrefix = $"{conversationId:N}:{inboundMessageId:N}:{MvpToolKeys.CreateGoogleCalendarReservation}:";
        var executions = await dbContext.ToolExecutions
            .AsNoTracking()
            .ForOrganization(organizationId)
            .Where(execution => execution.ConversationId == conversationId
                && execution.ToolKey == MvpToolKeys.CreateGoogleCalendarReservation
                && execution.Status == ToolExecutionStatus.ToolExecutionSucceeded
                && EF.Functions.Like(execution.IdempotencyKey, idempotencyPrefix + "%"))
            .OrderBy(execution => execution.CreatedAt)
            .ToListAsync(cancellationToken);

        foreach (var execution in executions)
        {
            await SendForExecutionAsync(
                execution,
                companyChannelId,
                recipientExternalId,
                cancellationToken);
        }
    }

    public async Task<bool> TryHandlePaymentReceiptAsync(
        Guid organizationId,
        Guid conversationId,
        Guid inboundMessageId,
        Guid companyChannelId,
        string recipientExternalId,
        MessageType inboundType,
        string? inboundText,
        CancellationToken cancellationToken)
    {
        var state = await dbContext.ConversationStates
            .ForOrganization(organizationId)
            .SingleOrDefaultAsync(entity => entity.ConversationId == conversationId, cancellationToken);

        if (state?.Snapshot.PendingAction != AwaitingPaymentConfirmation
            || !IsPaymentReceiptSignal(inboundType, inboundText))
        {
            return false;
        }

        state.Snapshot = WithReceiptReceived(state.Snapshot);
        var message = await CreateTextMessageAsync(
            organizationId,
            conversationId,
            ReceiptConfirmationText,
            $"payment-receipt:{inboundMessageId:N}",
            cancellationToken);

        await SendTextIfPendingAsync(
            message,
            organizationId,
            companyChannelId,
            conversationId,
            recipientExternalId,
            ReceiptConfirmationText,
            message.ProviderMessageId!,
            cancellationToken);
        await handoffExecutor.AutoEscalateAsync(organizationId, conversationId, inboundMessageId, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    private async Task SendForExecutionAsync(
        ToolExecution execution,
        Guid companyChannelId,
        string recipientExternalId,
        CancellationToken cancellationToken)
    {
        var paymentIdempotencyKey = $"payment:{execution.Id}";
        var existing = await dbContext.Messages
            .ForConversation(execution.OrganizationId, execution.ConversationId)
            .SingleOrDefaultAsync(
                message => message.ProviderMessageId == paymentIdempotencyKey,
                cancellationToken);

        if (existing?.Payload?.ProviderMessageId is { Length: > 0 })
        {
            return;
        }

        var account = await dbContext.CompanyPaymentAccounts
            .AsNoTracking()
            .Include(entity => entity.Bank)
            .ForOrganization(execution.OrganizationId)
            .Where(entity => entity.IsDefault && entity.IsActive)
            .OrderBy(entity => entity.Currency)
            .ThenBy(entity => entity.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (account is null || !IsConfigured(account))
        {
            await SendConfigurationMissingAsync(
                execution,
                companyChannelId,
                recipientExternalId,
                cancellationToken);
            return;
        }

        var caption = BuildPaymentCaption(account);
        var paymentMessage = existing ?? new Message
        {
            OrganizationId = execution.OrganizationId,
            ConversationId = execution.ConversationId,
            Role = MessageRole.Assistant,
            Type = MessageType.Image,
            MessageText = caption,
            ProviderMessageId = paymentIdempotencyKey,
            Payload = new MessagePayload
            {
                ProviderType = "image",
                BlobContainer = account.QrBlobContainer,
                BlobName = account.QrBlobName,
            },
            OccurredAt = timeProvider.GetUtcNow().UtcDateTime,
        };

        if (existing is null)
        {
            dbContext.Messages.Add(paymentMessage);
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        try
        {
            var qr = await qrImageProvider.GetQrImageAsync(account.QrBlobContainer, account.QrBlobName, cancellationToken);
            await outboundMessageDispatcher.SendImageAsync(
                new OutboundImageDispatchRequest(
                    execution.OrganizationId,
                    companyChannelId,
                    execution.ConversationId,
                    paymentMessage.Id,
                    recipientExternalId,
                    qr.Content,
                    qr.ContentType,
                    qr.FileName,
                    caption,
                    paymentIdempotencyKey),
                cancellationToken);
            await UpsertAwaitingPaymentStateAsync(execution, account, cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            PaymentQrImageSendFailed(logger, exception, execution.OrganizationId, execution.ConversationId, execution.Id);
            paymentMessage.Type = MessageType.Text;
            paymentMessage.Payload = new MessagePayload { ProviderType = "text" };
            await outboundMessageDispatcher.SendTextAsync(
                new OutboundTextDispatchRequest(
                    execution.OrganizationId,
                    companyChannelId,
                    execution.ConversationId,
                    paymentMessage.Id,
                    recipientExternalId,
                    caption,
                    paymentIdempotencyKey),
                cancellationToken);
            await UpsertAwaitingPaymentStateAsync(execution, account, cancellationToken);
            await handoffExecutor.AutoEscalateAsync(execution.OrganizationId, execution.ConversationId, execution.TriggerMessageId, cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    private async Task SendConfigurationMissingAsync(
        ToolExecution execution,
        Guid companyChannelId,
        string recipientExternalId,
        CancellationToken cancellationToken)
    {
        var message = await CreateTextMessageAsync(
            execution.OrganizationId,
            execution.ConversationId,
            PaymentFallbackText,
            $"payment-config-missing:{execution.Id}",
            cancellationToken);

        await SendTextIfPendingAsync(
            message,
            execution.OrganizationId,
            companyChannelId,
            execution.ConversationId,
            recipientExternalId,
            PaymentFallbackText,
            message.ProviderMessageId!,
            cancellationToken);

        await handoffExecutor.AutoEscalateAsync(execution.OrganizationId, execution.ConversationId, execution.TriggerMessageId, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task<Message> CreateTextMessageAsync(
        Guid organizationId,
        Guid conversationId,
        string text,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        var existing = await dbContext.Messages
            .ForConversation(organizationId, conversationId)
            .SingleOrDefaultAsync(message => message.ProviderMessageId == idempotencyKey, cancellationToken);
        if (existing is not null)
        {
            return existing;
        }

        var message = new Message
        {
            OrganizationId = organizationId,
            ConversationId = conversationId,
            Role = MessageRole.Assistant,
            Type = MessageType.Text,
            MessageText = text,
            ProviderMessageId = idempotencyKey,
            Payload = new MessagePayload
            {
                ProviderType = "text",
            },
            OccurredAt = timeProvider.GetUtcNow().UtcDateTime,
        };
        dbContext.Messages.Add(message);
        await dbContext.SaveChangesAsync(cancellationToken);
        return message;
    }

    private async Task UpsertAwaitingPaymentStateAsync(
        ToolExecution execution,
        CompanyPaymentAccount account,
        CancellationToken cancellationToken)
    {
        var state = await dbContext.ConversationStates
            .ForOrganization(execution.OrganizationId)
            .SingleOrDefaultAsync(entity => entity.ConversationId == execution.ConversationId, cancellationToken);
        var snapshot = new ConversationStateSnapshot
        {
            CurrentIntent = PaymentIntent,
            PendingAction = AwaitingPaymentConfirmation,
            Slots =
            [
                new ConversationSlot { Name = "payment_account_id", TextValue = account.Id.ToString("D") },
                new ConversationSlot { Name = "amount", NumberValue = account.ReservationPaymentAmount },
                new ConversationSlot { Name = "currency", TextValue = account.Currency },
                new ConversationSlot { Name = "reservation_event_id", TextValue = execution.Result?.CreateCalendarEvent?.EventId },
                new ConversationSlot { Name = "tool_execution_id", TextValue = execution.Id.ToString("D") },
            ],
            ConversationFlags = state?.Snapshot.ConversationFlags ?? [],
            TurnCount = state?.Snapshot.TurnCount ?? 0,
        };

        if (state is null)
        {
            dbContext.ConversationStates.Add(new ConversationState
            {
                OrganizationId = execution.OrganizationId,
                ConversationId = execution.ConversationId,
                Snapshot = snapshot,
            });
            return;
        }

        state.Snapshot = snapshot;
    }

    private static bool IsConfigured(CompanyPaymentAccount account)
    {
        return account.Bank.IsActive
            && !string.IsNullOrWhiteSpace(account.AccountNumber)
            && Enum.IsDefined(account.AccountType)
            && !string.IsNullOrWhiteSpace(account.Currency)
            && account.ReservationPaymentAmount > 0
            && !string.IsNullOrWhiteSpace(account.QrBlobContainer)
            && !string.IsNullOrWhiteSpace(account.QrBlobName);
    }

    private static string BuildPaymentCaption(CompanyPaymentAccount account)
    {
        return string.Join(
            Environment.NewLine,
            "Datos de pago para separar tu reserva:",
            FormattableString.Invariant($"Banco: {account.Bank.Name}"),
            FormattableString.Invariant($"Tipo de cuenta: {account.AccountType}"),
            FormattableString.Invariant($"Numero de cuenta: {account.AccountNumber}"),
            FormattableString.Invariant($"Monto: {account.ReservationPaymentAmount.ToString("0.##", CultureInfo.InvariantCulture)} {account.Currency}"));
    }

    private static bool IsPaymentReceiptSignal(MessageType inboundType, string? inboundText)
    {
        if (inboundType == MessageType.Image)
        {
            return true;
        }

        if (inboundType != MessageType.Text || string.IsNullOrWhiteSpace(inboundText))
        {
            return false;
        }

        var text = inboundText.Trim().ToLowerInvariant();
        return text.Contains("ya pag", StringComparison.Ordinal)
            || text.Contains("pague", StringComparison.Ordinal)
            || text.Contains("pagué", StringComparison.Ordinal)
            || text.Contains("pagado", StringComparison.Ordinal)
            || text.Contains("transfer", StringComparison.Ordinal)
            || text.Contains("comprobante", StringComparison.Ordinal)
            || text.Contains("recibo", StringComparison.Ordinal)
            || text.Contains("consign", StringComparison.Ordinal)
            || text.Contains("deposit", StringComparison.Ordinal);
    }

    private static ConversationStateSnapshot WithReceiptReceived(ConversationStateSnapshot current)
    {
        var flags = new List<string>(current.ConversationFlags);
        if (!flags.Contains(ReceiptReceived, StringComparer.Ordinal))
        {
            flags.Add(ReceiptReceived);
        }

        return new ConversationStateSnapshot
        {
            CurrentIntent = current.CurrentIntent,
            PendingAction = ReceiptReceived,
            Slots = current.Slots,
            ConversationFlags = flags,
            TurnCount = current.TurnCount,
        };
    }

    private async Task SendTextIfPendingAsync(
        Message message,
        Guid organizationId,
        Guid companyChannelId,
        Guid conversationId,
        string recipientExternalId,
        string text,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        if (message.Payload?.ProviderMessageId is { Length: > 0 })
        {
            return;
        }

        await outboundMessageDispatcher.SendTextAsync(
            new OutboundTextDispatchRequest(
                organizationId,
                companyChannelId,
                conversationId,
                message.Id,
                recipientExternalId,
                text,
                idempotencyKey),
            cancellationToken);
    }

    [LoggerMessage(
        EventId = 6101,
        Level = LogLevel.Warning,
        Message = "PaymentQrImageSendFailed OrganizationId={OrganizationId} ConversationId={ConversationId} ToolExecutionId={ToolExecutionId}")]
    private static partial void PaymentQrImageSendFailed(
        ILogger logger,
        Exception exception,
        Guid organizationId,
        Guid conversationId,
        Guid toolExecutionId);
}
