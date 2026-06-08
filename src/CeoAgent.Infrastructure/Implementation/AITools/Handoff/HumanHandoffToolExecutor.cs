using CeoAgent.Application;
using CeoAgent.Application.Abstractions.Messaging;
using CeoAgent.Infrastructure.Entities;
using CeoAgent.Infrastructure.Entities.JsonDocuments;
using CeoAgent.Infrastructure.Persistence.Extensions;
using CeoAgent.Shared.Constants;
using CeoAgent.Shared.Enums;
using CeoAgent.Shared.Messaging;
using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CeoAgent.Infrastructure.Implementation.AITools.Handoff;

/// <summary>
/// Applies human handoff escalation: silences the bot by moving the conversation to
/// <see cref="ConversationStatus.HandedOff"/>, records an idempotent <see cref="ToolExecution"/>,
/// updates conversation state, and pushes a sanitized staff notification.
/// </summary>
public sealed class HumanHandoffToolExecutor(
    CeoAgentDbContext dbContext,
    IMessageChannelIntegration messaging,
    TimeProvider timeProvider,
    ILogger<HumanHandoffToolExecutor> logger)
{
    private const string HandoffIntent = "human_handoff_request";
    private const string HumanRequestedFlag = "human_requested";
    private const string WhatsAppProvider = "whatsapp_cloud";
    private const string NotificationUnavailableReason = "notification_unavailable";
    private const string AutoEscalationReason = "agent_loop_exhausted";

    private static readonly EventId HandoffEscalatedEvent = new(3101, "HumanHandoffEscalated");
    private static readonly EventId HandoffNotificationFailedEvent = new(3102, "HumanHandoffNotificationUnavailable");
    private static readonly EventId HandoffToolMissingEvent = new(3103, "HumanHandoffToolNotConfigured");

    /// <summary>
    /// Tool-driven escalation. Invoked from the agent loop through the request_human_handoff tool executor.
    /// </summary>
    public async Task<ToolExecution> RequestHandoffAsync(
        Guid companyId,
        Guid conversationId,
        Guid companyToolId,
        Guid triggerMessageId,
        RequestHumanHandoffRequest request,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var existing = await dbContext.FindTrackedOrPersistedToolExecutionAsync(companyId, idempotencyKey, cancellationToken);
        if (existing is not null)
        {
            return existing;
        }

        var tool = await dbContext.CompanyTools
            .EnabledForCompanyTool(companyId, companyToolId)
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new InvalidOperationException($"Company tool '{companyToolId}' was not found or is not enabled.");

        var config = tool.Configuration?.RequestHumanHandoff
            ?? throw new InvalidOperationException("request_human_handoff tool configuration is required.");

        return await ApplyHandoffAsync(
            companyId,
            conversationId,
            tool.Id,
            triggerMessageId,
            request,
            config,
            idempotencyKey,
            cancellationToken);
    }

    /// <summary>
    /// Non-tool escalation used when the agent loop is exhausted. Silences the bot and notifies staff.
    /// Persists a <see cref="ToolExecution"/> only when the company has an enabled request_human_handoff tool.
    /// </summary>
    public async Task<bool> AutoEscalateAsync(
        Guid companyId,
        Guid conversationId,
        Guid triggerMessageId,
        CancellationToken cancellationToken)
    {
        var idempotencyKey = $"{conversationId:N}:{triggerMessageId:N}:auto_human_handoff";
        var existing = await dbContext.FindTrackedOrPersistedToolExecutionAsync(companyId, idempotencyKey, cancellationToken);
        if (existing is not null)
        {
            return true;
        }

        var tool = await dbContext.CompanyTools
            .EnabledForCompany(companyId)
            .Where(entity => entity.ToolKey == MvpToolKeys.RequestHumanHandoff)
            .SingleOrDefaultAsync(cancellationToken);

        if (tool?.Configuration?.RequestHumanHandoff is { } config)
        {
            await ApplyHandoffAsync(
                companyId,
                conversationId,
                tool.Id,
                triggerMessageId,
                new RequestHumanHandoffRequest { Reason = AutoEscalationReason },
                config,
                idempotencyKey,
                cancellationToken);
            return true;
        }

        // No request_human_handoff tool configured: still silence the bot (HandedOff is the single source
        // of truth) and emit an observable, sanitized signal, but skip ToolExecution persistence because it
        // requires an enabled company tool. Documented in docs/human-handoff.md.
        logger.LogWarning(
            HandoffToolMissingEvent,
            "HumanHandoffToolNotConfigured CompanyId={CompanyId} ConversationId={ConversationId} Reason={Reason}",
            companyId,
            conversationId,
            AutoEscalationReason);

        var conversation = await LoadConversationAsync(companyId, conversationId, cancellationToken);
        conversation.Status = ConversationStatus.HandedOff;
        await UpsertHandoffStateAsync(companyId, conversationId, cancellationToken);
        return true;
    }

    private async Task<ToolExecution> ApplyHandoffAsync(
        Guid companyId,
        Guid conversationId,
        Guid companyToolId,
        Guid triggerMessageId,
        RequestHumanHandoffRequest request,
        RequestHumanHandoffConfig config,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        var conversation = await LoadConversationAsync(companyId, conversationId, cancellationToken);
        conversation.Status = ConversationStatus.HandedOff;

        await UpsertHandoffStateAsync(companyId, conversationId, cancellationToken);

        var now = timeProvider.GetUtcNow();
        var handoffTicketId = Guid.CreateVersion7().ToString("N");
        var estimatedPickupAt = config.TimeoutMinutes > 0
            ? now.AddMinutes(config.TimeoutMinutes)
            : (DateTimeOffset?)null;

        var result = new RequestHumanHandoffResult
        {
            HandoffRequested = true,
            HandoffTicketId = handoffTicketId,
            EstimatedPickupAt = estimatedPickupAt,
        };

        var resultMessage = new Message
        {
            CompanyId = companyId,
            ConversationId = conversationId,
            Role = MessageRole.ToolResult,
            Type = MessageType.Text,
            MessageText = MvpToolKeys.RequestHumanHandoff,
            OccurredAt = now.UtcDateTime,
        };
        dbContext.Messages.Add(resultMessage);

        var execution = new ToolExecution
        {
            CompanyId = companyId,
            ConversationId = conversationId,
            CompanyToolId = companyToolId,
            TriggerMessageId = triggerMessageId,
            ToolKey = MvpToolKeys.RequestHumanHandoff,
            IdempotencyKey = idempotencyKey,
            Status = ToolExecutionStatus.Succeeded,
            Request = ToolExecutionRequest.ForRequestHumanHandoff(request),
            Result = ToolExecutionResult.ForRequestHumanHandoff(result),
            ResultMessageId = resultMessage.Id,
        };

        var notified = await PushStaffNotificationAsync(
            companyId,
            conversationId,
            conversation.CompanyChannelId,
            triggerMessageId,
            handoffTicketId,
            request.Reason,
            estimatedPickupAt,
            config,
            cancellationToken);

        if (!notified)
        {
            // Push could not be delivered over WhatsApp. The handoff still stands (HandedOff + pull view),
            // but the failure is made observable per existing tool patterns.
            execution.FailureReason = NotificationUnavailableReason;
        }

        dbContext.ToolExecutions.Add(execution);
        return execution;
    }

    private async Task<Conversation> LoadConversationAsync(
        Guid companyId,
        Guid conversationId,
        CancellationToken cancellationToken)
    {
        return await dbContext.Conversations
            .ForCompany(companyId)
            .SingleOrDefaultAsync(entity => entity.Id == conversationId, cancellationToken)
            ?? throw new InvalidOperationException($"Conversation '{conversationId}' was not found.");
    }

    private async Task UpsertHandoffStateAsync(
        Guid companyId,
        Guid conversationId,
        CancellationToken cancellationToken)
    {
        var state = await dbContext.ConversationStates
            .ForCompany(companyId)
            .SingleOrDefaultAsync(entity => entity.ConversationId == conversationId, cancellationToken);

        if (state is null)
        {
            dbContext.ConversationStates.Add(new ConversationState
            {
                CompanyId = companyId,
                ConversationId = conversationId,
                Snapshot = new ConversationStateSnapshot
                {
                    CurrentIntent = HandoffIntent,
                    ConversationFlags = [HumanRequestedFlag],
                },
            });
            return;
        }

        var flags = new List<string>(state.Snapshot.ConversationFlags);
        if (!flags.Contains(HumanRequestedFlag, StringComparer.Ordinal))
        {
            flags.Add(HumanRequestedFlag);
        }

        // Reassign the complex property so the JSON column is detected as modified.
        state.Snapshot = new ConversationStateSnapshot
        {
            CurrentIntent = HandoffIntent,
            PendingAction = state.Snapshot.PendingAction,
            Slots = state.Snapshot.Slots,
            ConversationFlags = flags,
            TurnCount = state.Snapshot.TurnCount,
        };
    }

    private async Task<bool> PushStaffNotificationAsync(
        Guid companyId,
        Guid conversationId,
        Guid companyChannelId,
        Guid triggerMessageId,
        string handoffTicketId,
        string reason,
        DateTimeOffset? estimatedPickupAt,
        RequestHumanHandoffConfig config,
        CancellationToken cancellationToken)
    {
        var eta = estimatedPickupAt?.ToString("u", CultureInfo.InvariantCulture) ?? "unscheduled";
        var alert =
            $"Atencion humana requerida. Ticket: {handoffTicketId}. ConversationId: {conversationId}. " +
            $"Motivo: {reason}. Canal: WhatsApp. ETA: {eta}.";

        // Observable, sanitized push (no PII): always emitted as the MVP staff signal.
        logger.LogInformation(
            HandoffEscalatedEvent,
            "HumanHandoffEscalated CompanyId={CompanyId} ConversationId={ConversationId} CompanyChannelId={CompanyChannelId} Provider={Provider} HandoffTicketId={HandoffTicketId} Reason={Reason} EstimatedPickupAt={EstimatedPickupAt} EscalationChannel={EscalationChannel} NotifyUserCount={NotifyUserCount}",
            companyId,
            conversationId,
            companyChannelId,
            WhatsAppProvider,
            handoffTicketId,
            reason,
            eta,
            config.EscalationChannel,
            config.NotifyUsers.Count);

        CeoAgentTelemetry.HumanHandoffEscalations.Add(1);

        var recipients = config.NotifyUsers
            .Select(NormalizeRecipient)
            .Where(recipient => recipient is not null)
            .Select(recipient => recipient!)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        if (recipients.Length == 0)
        {
            // NotifyUsers contains no WhatsApp-supported recipients for the current port (free-form text).
            // Documented limitation in docs/human-handoff.md.
            CeoAgentTelemetry.HumanHandoffNotificationsUnavailable.Add(1);
            return false;
        }

        var delivered = false;
        foreach (var recipient in recipients)
        {
            try
            {
                await messaging.SendTextAsync(
                    new ChannelTextMessage(
                        companyId,
                        companyChannelId,
                        conversationId,
                        triggerMessageId,
                        recipient,
                        alert,
                        $"handoff-notify:{handoffTicketId}:{recipient}"),
                    cancellationToken);
                delivered = true;
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                logger.LogWarning(
                    HandoffNotificationFailedEvent,
                    exception,
                    "HumanHandoffNotificationUnavailable CompanyId={CompanyId} ConversationId={ConversationId} HandoffTicketId={HandoffTicketId}",
                    companyId,
                    conversationId,
                    handoffTicketId);
            }
        }

        if (!delivered)
        {
            CeoAgentTelemetry.HumanHandoffNotificationsUnavailable.Add(1);
        }

        return delivered;
    }

    private static string? NormalizeRecipient(string? notifyUser)
    {
        if (string.IsNullOrWhiteSpace(notifyUser))
        {
            return null;
        }

        var trimmed = notifyUser.Trim();
        var digits = trimmed.StartsWith('+') ? trimmed[1..] : trimmed;

        // Only E.164-style phone numbers are deliverable as WhatsApp free-form text by the current port.
        if (digits.Length is < 8 or > 15 || !digits.All(char.IsAsciiDigit))
        {
            return null;
        }

        return digits;
    }
}
