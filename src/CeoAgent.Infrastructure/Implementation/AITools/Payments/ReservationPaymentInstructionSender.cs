using CeoAgent.Application.Abstractions.AITools;
using CeoAgent.Infrastructure.Entities;
using CeoAgent.Infrastructure.Entities.JsonDocuments;
using CeoAgent.Infrastructure.Implementation.AITools.Handoff;
using CeoAgent.Infrastructure.Persistence.Extensions;
using CeoAgent.Shared.Constants;
using CeoAgent.Shared.Enums;

namespace CeoAgent.Infrastructure.Implementation.AITools.Payments;

public sealed class ReservationPaymentInstructionSender(
    CeoAgentDbContext dbContext,
    PaymentInstructionDataReader dataReader,
    PaymentInstructionDispatchService dispatchService,
    HumanHandoffToolExecutor handoffExecutor,
    TimeProvider timeProvider)
{
    private const string PaymentFallbackText = "La reserva quedo creada, pero necesito que una persona del equipo te comparta la informacion de pago.";
    private const string ReservationNotFoundFailureReason = "reservation_not_found";
    private const string PaymentAccountNotConfiguredFailureReason = "payment_account_not_configured";

    public async Task<ToolExecution> SendForLatestSuccessfulReservationAsync(
        ToolExecutionContext executionContext,
        SendPaymentInstructionsRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var existing = await dbContext.FindTrackedOrPersistedToolExecutionAsync(
            executionContext.OrganizationId,
            executionContext.IdempotencyKey,
            cancellationToken);
        if (existing is not null)
        {
            return existing;
        }

        var context = await dataReader.LoadToolContextAsync(executionContext, cancellationToken);
        var reservationExecution = await dataReader.FindLatestSuccessfulReservationExecutionAsync(context, cancellationToken);
        if (reservationExecution is null)
        {
            return await PersistPaymentExecutionAsync(
                context,
                executionContext.TriggerMessageId,
                executionContext.IdempotencyKey,
                request,
                ToolExecutionStatus.ToolExecutionDenied,
                ReservationNotFoundFailureReason,
                new SendPaymentInstructionsResult
                {
                    PaymentInstructionsSent = false,
                    CustomerVisibleMessageSent = false,
                    HandoffRequested = false,
                },
                cancellationToken);
        }

        var execution = await PersistPaymentExecutionAsync(
            context,
            executionContext.TriggerMessageId,
            executionContext.IdempotencyKey,
            request,
            ToolExecutionStatus.ToolExecutionInProgress,
            failureReason: null,
            result: null,
            cancellationToken);

        var account = await dataReader.FindDefaultActivePaymentAccountAsync(
            reservationExecution.OrganizationId,
            cancellationToken);

        PaymentInstructionDispatchOutcome outcome;
        bool handoffRequested;
        if (account is null || !PaymentInstructionDataReader.IsConfigured(account))
        {
            var fallback = await dispatchService.SendFallbackTextAsync(
                reservationExecution.OrganizationId,
                reservationExecution.ConversationId,
                context.Conversation.CompanyChannelId,
                context.Customer.ExternalCustomerId,
                PaymentFallbackText,
                $"payment-config-missing:{execution.Id:N}",
                cancellationToken);

            handoffRequested = fallback.CustomerVisibleMessageSent
                && await handoffExecutor.AutoEscalateAsync(
                    reservationExecution.OrganizationId,
                    reservationExecution.ConversationId,
                    execution.TriggerMessageId,
                    cancellationToken);
            outcome = new PaymentInstructionDispatchOutcome(
                PaymentInstructionsSent: false,
                CustomerVisibleMessageSent: fallback.CustomerVisibleMessageSent,
                PaymentMessageId: fallback.MessageId,
                ToolExecutionStatus.ToolExecutionFailed,
                PaymentAccountNotConfiguredFailureReason);
        }
        else
        {
            var caption = PaymentInstructionCaptionBuilder.Build(account, reservationExecution);
            outcome = await dispatchService.SendPaymentImageAsync(
                reservationExecution,
                account,
                context.Conversation.CompanyChannelId,
                context.Customer.ExternalCustomerId,
                caption,
                cancellationToken);
            handoffRequested = outcome.CustomerVisibleMessageSent
                && await handoffExecutor.AutoEscalateAsync(
                    reservationExecution.OrganizationId,
                    reservationExecution.ConversationId,
                    execution.TriggerMessageId,
                    cancellationToken);
        }

        execution.Status = outcome.Status;
        execution.FailureReason = outcome.FailureReason;
        execution.Result = ToolExecutionResult.ForSendPaymentInstructions(new SendPaymentInstructionsResult
        {
            PaymentInstructionsSent = outcome.PaymentInstructionsSent,
            CustomerVisibleMessageSent = outcome.CustomerVisibleMessageSent,
            HandoffRequested = handoffRequested,
            ReservationEventId = reservationExecution.Result?.CreateCalendarEvent?.EventId,
            PaymentMessageId = outcome.PaymentMessageId,
        });
        EnsureToolResultMessage(execution);
        await dbContext.SaveChangesAsync(cancellationToken);
        return execution;
    }

    private async Task<ToolExecution> PersistPaymentExecutionAsync(
        PaymentInstructionToolContext context,
        Guid triggerMessageId,
        string idempotencyKey,
        SendPaymentInstructionsRequest request,
        ToolExecutionStatus status,
        string? failureReason,
        SendPaymentInstructionsResult? result,
        CancellationToken cancellationToken)
    {
        var execution = new ToolExecution
        {
            OrganizationId = context.Conversation.OrganizationId,
            ConversationId = context.Conversation.Id,
            CompanyToolId = context.Tool.Id,
            TriggerMessageId = triggerMessageId,
            ToolKey = MvpToolKeys.SendPaymentInstructions,
            IdempotencyKey = idempotencyKey,
            Status = status,
            Request = ToolExecutionRequest.ForSendPaymentInstructions(request),
            Result = result is null ? null : ToolExecutionResult.ForSendPaymentInstructions(result),
            FailureReason = failureReason,
        };

        if (result is not null)
        {
            EnsureToolResultMessage(execution);
        }

        dbContext.ToolExecutions.Add(execution);
        await dbContext.SaveChangesAsync(cancellationToken);
        return execution;
    }

    private void EnsureToolResultMessage(ToolExecution execution)
    {
        if (execution.ResultMessageId is not null)
        {
            return;
        }

        var resultMessage = new Message
        {
            OrganizationId = execution.OrganizationId,
            ConversationId = execution.ConversationId,
            Role = MessageRole.ToolResult,
            Type = MessageType.Text,
            MessageText = execution.ToolKey,
            OccurredAt = timeProvider.GetUtcNow().UtcDateTime,
        };
        dbContext.Messages.Add(resultMessage);
        execution.ResultMessageId = resultMessage.Id;
    }
}
