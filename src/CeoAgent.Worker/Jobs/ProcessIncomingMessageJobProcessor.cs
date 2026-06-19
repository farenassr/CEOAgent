using CeoAgent.Application.Agents;
using CeoAgent.Application.Abstractions.Organization;
using CeoAgent.Infrastructure;
using CeoAgent.Infrastructure.Entities;
using CeoAgent.Infrastructure.Entities.JsonDocuments;
using CeoAgent.Application.Abstractions.AI;
using CeoAgent.Infrastructure.Persistence.Extensions;
using CeoAgent.Shared.AI;
using CeoAgent.Application.Abstractions.Jobs;
using CeoAgent.Shared.Jobs;
using CeoAgent.Application.Abstractions.Messaging;
using CeoAgent.Shared.Messaging;
using CeoAgent.Shared.Enums;
using CeoAgent.Shared.Prompt;
using CeoAgent.Infrastructure.Implementation.AITools.Handoff;
using CeoAgent.Worker.Jobs.Telemetry;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using System.Diagnostics;

namespace CeoAgent.Worker.Jobs;

/// <summary>
/// Processes an inbound message job by loading conversation context, invoking the agent, and sending the reply.
/// </summary>
public sealed partial class ProcessIncomingMessageJobProcessor(
    CeoAgentDbContext dbContext,
    IMessageChannelIntegration messaging,
    IOutboundMessageDispatcher outboundMessageDispatcher,
    IAgentRuntime agentRuntime,
    HumanHandoffToolExecutor handoffExecutor,
    ReservationPaymentInstructionSender paymentInstructionSender,
    IOrganizationContextAccessor companyContextAccessor,
    TimeProvider timeProvider,
    IHostEnvironment hostEnvironment,
    ILogger<ProcessIncomingMessageJobProcessor> logger)
{
    private const string WhatsAppProvider = "whatsapp_cloud";
    private const string UnsupportedMessageText = "Por ahora solo puedo procesar mensajes de texto.";
    private const string LoopCapFallbackText = "No pude completar la accion automatica. Te pondre en contacto con una persona del equipo.";
    private const string LlmBudgetGuardActiveFailureReason = "llm_budget_guard_active";

    /// <summary>
    /// Runs the inbound message workflow, including read receipts, prompt creation, and outbound response delivery.
    /// </summary>
    public async Task ProcessAsync(ProcessIncomingMessageJob job, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(job);

        using var processActivity = ProcessIncomingMessageJobTelemetry.StartMessageProcessing(job, WhatsAppProvider);
        using var logScope = BeginJobScope(job);
        companyContextAccessor.SetOrganization(job.OrganizationId);

        try
        {
            var context = await LoadContextAsync(job, cancellationToken);
            ProcessIncomingMessageJobTelemetry.EnrichMessageProcessing(processActivity, CreateTelemetryContext(context));

            if (context.Conversation.Status == ConversationStatus.HandedOff)
            {
                // HandedOff is the single source of truth for pausing the agent. The inbound message is
                // already persisted by webhook ingestion; we optionally acknowledge it with a read receipt
                // and exit without building a prompt or running the agent loop.
                if (ShouldMarkInboundRead(context.Inbound))
                {
                    await messaging.MarkMessageReadAsync(
                        new ChannelMessageReference(
                            context.Company.Id,
                            context.Channel.Id,
                            WhatsAppProvider,
                            context.Inbound.ProviderMessageId!),
                        cancellationToken);
                }

                InboundSuppressedDuringHandoff(
                    logger,
                    context.Inbound.Id);
                return;
            }

            var replyClientMessageId = ReplyClientMessageId(context.Inbound);

            var existingReply = await dbContext.Messages
                .AssistantReplyForClientMessageId(context.Company.Id, context.Conversation.Id, replyClientMessageId)
                .SingleOrDefaultAsync(cancellationToken);

            if (existingReply is not null)
            {
                if (!string.IsNullOrEmpty(existingReply.Payload?.ProviderMessageId))
                {
                    await paymentInstructionSender.SendForSuccessfulReservationsAsync(
                        context.Company.Id,
                        context.Conversation.Id,
                        context.Inbound.Id,
                        context.Channel.Id,
                        context.Customer.ExternalCustomerId,
                        cancellationToken);
                    return;
                }

                await SendExistingReplyAsync(context, existingReply, replyClientMessageId, job.CorrelationId, cancellationToken);
                await paymentInstructionSender.SendForSuccessfulReservationsAsync(
                    context.Company.Id,
                    context.Conversation.Id,
                    context.Inbound.Id,
                    context.Channel.Id,
                    context.Customer.ExternalCustomerId,
                    cancellationToken);
                return;
            }

            if (ShouldMarkInboundRead(context.Inbound))
            {
                await messaging.MarkMessageReadAsync(
                    new ChannelMessageReference(
                        context.Company.Id,
                        context.Channel.Id,
                        WhatsAppProvider,
                        context.Inbound.ProviderMessageId!),
                    cancellationToken);
            }

            if (await paymentInstructionSender.TryHandlePaymentReceiptAsync(
                context.Company.Id,
                context.Conversation.Id,
                context.Inbound.Id,
                context.Channel.Id,
                context.Customer.ExternalCustomerId,
                context.Inbound.Type,
                context.Inbound.MessageText,
                cancellationToken))
            {
                return;
            }

            if (context.Inbound.Type != MessageType.Text)
            {
                await SendTextReplyAsync(context, UnsupportedMessageText, replyClientMessageId, job, cancellationToken);
                return;
            }

            var prompt = BuildPrompt(context);
            var agentText = await RunAgentTurnAsync(context, prompt, job.CorrelationId, cancellationToken);
            var assistantText = string.IsNullOrWhiteSpace(agentText)
                ? string.Empty
                : agentText.Trim();

            var assistant = new Message
            {
                OrganizationId = context.Company.Id,
                ConversationId = context.Conversation.Id,
                Role = MessageRole.Assistant,
                Type = MessageType.Text,
                MessageText = assistantText,
                ProviderMessageId = replyClientMessageId,
                Payload = new MessagePayload
                {
                    ProviderType = "text",
                },
                OccurredAt = timeProvider.GetUtcNow().UtcDateTime,
            };

            dbContext.Messages.Add(assistant);
            context.Conversation.LastMessageAt = timeProvider.GetUtcNow().UtcDateTime;

            await outboundMessageDispatcher.SendTextAsync(
                new OutboundTextDispatchRequest(
                    context.Company.Id,
                    context.Channel.Id,
                    context.Conversation.Id,
                    assistant.Id,
                    context.Customer.ExternalCustomerId,
                    assistantText,
                    replyClientMessageId,
                    job.CorrelationId),
                cancellationToken);
            await paymentInstructionSender.SendForSuccessfulReservationsAsync(
                context.Company.Id,
                context.Conversation.Id,
                context.Inbound.Id,
                context.Channel.Id,
                context.Customer.ExternalCustomerId,
                cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            ProcessIncomingMessageJobTelemetry.MarkError(processActivity, exception);
            throw;
        }
        finally
        {
            companyContextAccessor.Clear();
        }
    }

    private static bool ShouldMarkInboundRead(Message inbound)
    {
        return !string.IsNullOrWhiteSpace(inbound.ProviderMessageId)
            && inbound.ProviderMessageId.StartsWith("wamid.", StringComparison.OrdinalIgnoreCase);
    }

    private async Task SendTextReplyAsync(
        ProcessorContext context,
        string text,
        string replyClientMessageId,
        ProcessIncomingMessageJob job,
        CancellationToken cancellationToken)
    {
        var assistant = new Message
        {
            OrganizationId = context.Company.Id,
            ConversationId = context.Conversation.Id,
            Role = MessageRole.Assistant,
            Type = MessageType.Text,
            MessageText = text,
            ProviderMessageId = replyClientMessageId,
            Payload = new MessagePayload
            {
                ProviderType = "text",
            },
            OccurredAt = timeProvider.GetUtcNow().UtcDateTime,
        };

        dbContext.Messages.Add(assistant);
        context.Conversation.LastMessageAt = timeProvider.GetUtcNow().UtcDateTime;
        await outboundMessageDispatcher.SendTextAsync(
            new OutboundTextDispatchRequest(
                context.Company.Id,
                context.Channel.Id,
                context.Conversation.Id,
                assistant.Id,
                context.Customer.ExternalCustomerId,
                text,
                replyClientMessageId,
                job.CorrelationId),
            cancellationToken);
    }

    private async Task SendExistingReplyAsync(
        ProcessorContext context,
        Message existingReply,
        string replyClientMessageId,
        string? correlationId,
        CancellationToken cancellationToken)
    {
        context.Conversation.LastMessageAt = timeProvider.GetUtcNow().UtcDateTime;
        await outboundMessageDispatcher.SendTextAsync(
            new OutboundTextDispatchRequest(
                context.Company.Id,
                context.Channel.Id,
                context.Conversation.Id,
                existingReply.Id,
                context.Customer.ExternalCustomerId,
                existingReply.MessageText ?? string.Empty,
                replyClientMessageId,
                correlationId),
            cancellationToken);
    }

    private static string ReplyClientMessageId(Message inbound)
    {
        return $"reply:{inbound.Id}";
    }

    private static ProcessIncomingMessageTelemetryContext CreateTelemetryContext(ProcessorContext context)
    {
        return new ProcessIncomingMessageTelemetryContext(
            context.Company.Id,
            context.Conversation.Id,
            context.AgentProfile.Id,
            context.Channel.Provider.ToString(),
            EffectiveProvider(context).ToString(),
            EffectiveModelName(context),
            ToolCount: 0);
    }

    private async Task<ProcessorContext> LoadContextAsync(ProcessIncomingMessageJob job, CancellationToken cancellationToken)
    {
        var company = await dbContext.Companies.AsNoTracking().SingleOrDefaultAsync(entity => entity.Id == job.OrganizationId, cancellationToken)
            ?? throw new InvalidOperationException($"Company '{job.OrganizationId}' was not found.");
        var conversation = await dbContext.Conversations
            .ForOrganization(job.OrganizationId)
            .Include(entity => entity.Customer)
            .Include(entity => entity.CompanyChannel)
            .Include(entity => entity.AgentProfile)
            .SingleAsync(entity => entity.Id == job.ConversationId, cancellationToken);
        var agentProfile = conversation.AgentProfile;
        var channel = conversation.CompanyChannel;
        var customer = conversation.Customer;
        DetachReadOnlyContextEntity(agentProfile);
        DetachReadOnlyContextEntity(channel);
        DetachReadOnlyContextEntity(customer);

        var inbound = await dbContext.Messages
            .AsNoTracking()
            .ForConversation(job.OrganizationId, job.ConversationId)
            .SingleOrDefaultAsync(
                entity => entity.Id == job.MessageId,
                cancellationToken)
            ?? throw new InvalidOperationException($"Message '{job.MessageId}' was not found.");

        return new ProcessorContext(
            company,
            agentProfile,
            conversation,
            channel,
            customer,
            inbound);
    }

    private void DetachReadOnlyContextEntity<TEntity>(TEntity entity)
        where TEntity : class
    {
        dbContext.Entry(entity).State = EntityState.Detached;
    }

    private async Task<string?> RunAgentTurnAsync(
        ProcessorContext context,
        string prompt,
        string? correlationId,
        CancellationToken cancellationToken)
    {
        var telemetryContext = CreateTelemetryContext(context);
        var maxEstimatedCostUsd = context.AgentProfile.MaxEstimatedCostUsdPerJob;
        var provider = EffectiveProvider(context);
        var modelName = EffectiveModelName(context);
        var mutatingToolsEnabled = AreMutatingToolsEnabledForTurn(maxEstimatedCostUsd);
        var mutatingToolsDisabledReason = mutatingToolsEnabled ? null : LlmBudgetGuardActiveFailureReason;

        if (maxEstimatedCostUsd > 0
            && !IsLocalDevelopmentOrTesting()
            && !agentRuntime.CanEstimateCost(provider, modelName))
        {
            ProcessIncomingMessageJobTelemetry.RecordLlmCostEstimationMissing();
            LlmCostPricingMissing(
                logger,
                context.Company.Id,
                context.AgentProfile.Id,
                modelName,
                hostEnvironment.EnvironmentName);
            await EscalateToHumanAsync(context, cancellationToken);
            return LoopCapFallbackText;
        }

        AgentTurnResult agentResult;
        using var iterationActivity = ProcessIncomingMessageJobTelemetry.StartAgentIteration(telemetryContext, 0);
        using (var activity = ProcessIncomingMessageJobTelemetry.StartLlmGeneration(telemetryContext, 0))
        {
            var stopwatch = Stopwatch.StartNew();
            try
            {
                agentResult = await agentRuntime.RunTurnAsync(
                    new AgentTurnRequest(
                        context.Company.Id,
                        context.Conversation.Id,
                        context.Inbound.Id,
                        provider,
                        modelName,
                        prompt,
                        context.Inbound.MessageText ?? string.Empty,
                        context.AgentProfile.MaxOutputTokenCount,
                        correlationId,
                        mutatingToolsEnabled,
                        mutatingToolsDisabledReason),
                    cancellationToken);
                stopwatch.Stop();
                ProcessIncomingMessageJobTelemetry.RecordLlmDuration(stopwatch.Elapsed);
                ProcessIncomingMessageJobTelemetry.RecordTokenUsage(agentResult);
                if (agentResult.EstimatedCostUsd is null && maxEstimatedCostUsd > 0)
                {
                    ProcessIncomingMessageJobTelemetry.RecordLlmCostEstimationMissing();
                }

                ProcessIncomingMessageJobTelemetry.EnrichLlmGenerationResult(
                    activity,
                    agentResult,
                    modelName);
                ProcessIncomingMessageJobTelemetry.MarkOk(activity);
                ProcessIncomingMessageJobTelemetry.MarkOk(iterationActivity);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                stopwatch.Stop();
                ProcessIncomingMessageJobTelemetry.RecordLlmDuration(stopwatch.Elapsed);
                ProcessIncomingMessageJobTelemetry.MarkError(activity, exception);
                ProcessIncomingMessageJobTelemetry.MarkError(iterationActivity, exception);
                AgentRuntimeFailed(
                    logger,
                    exception,
                    0);
                await EscalateToHumanAsync(context, cancellationToken);
                return LoopCapFallbackText;
            }
        }

        if (agentResult.EstimatedCostUsd is { } estimatedCost
            && IsLlmBudgetExceeded(maxEstimatedCostUsd, estimatedCost))
        {
            ProcessIncomingMessageJobTelemetry.RecordLlmBudgetExceeded();
            LlmBudgetExceeded(
                logger,
                context.Company.Id,
                context.AgentProfile.Id,
                modelName,
                estimatedCost,
                maxEstimatedCostUsd);
            await EscalateToHumanAsync(context, cancellationToken);
            return LoopCapFallbackText;
        }

        return agentResult.AssistantText;
    }

    /// <summary>
    /// Escalates the conversation to a human when the agent loop cannot complete. Moves the conversation
    /// to HandedOff and notifies staff so the fallback promise ("te pondre en contacto con una persona")
    /// is backed by an actual handoff. The single confirmation reply is still sent by the caller.
    /// </summary>
    private async Task EscalateToHumanAsync(ProcessorContext context, CancellationToken cancellationToken)
    {
        try
        {
            await handoffExecutor.AutoEscalateAsync(
                context.Company.Id,
                context.Conversation.Id,
                context.Inbound.Id,
                cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            AutoHandoffEscalationFailed(
                logger,
                exception);
        }
    }

    private DateTimeOffset ToCompanyLocalNow(string timeZoneId)
    {
        var timeZone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        return TimeZoneInfo.ConvertTime(timeProvider.GetUtcNow(), timeZone);
    }

    private bool IsLocalDevelopmentOrTesting()
    {
        return hostEnvironment.IsDevelopment()
            || string.Equals(hostEnvironment.EnvironmentName, "Local", StringComparison.OrdinalIgnoreCase)
            || string.Equals(hostEnvironment.EnvironmentName, "Testing", StringComparison.OrdinalIgnoreCase);
    }

    private bool AreMutatingToolsEnabledForTurn(double maxEstimatedCostUsd)
    {
        return maxEstimatedCostUsd <= 0 || IsLocalDevelopmentOrTesting();
    }

    private static bool IsLlmBudgetExceeded(double maxEstimatedCostUsd, double accumulatedEstimatedCostUsd)
    {
        return maxEstimatedCostUsd > 0 && accumulatedEstimatedCostUsd >= maxEstimatedCostUsd;
    }

    private sealed record ProcessorContext(
        Company Company,
        AgentProfile AgentProfile,
        Conversation Conversation,
        CompanyChannel Channel,
        Customer Customer,
        Message Inbound);

    private string BuildPrompt(ProcessorContext context)
    {
        return AgentPromptBuilder.Build(new AgentPromptContext
        {
            CompanyName = context.Company.Name,
            TimeZoneId = context.Company.TimeZoneId,
            LocalNow = ToCompanyLocalNow(context.Company.TimeZoneId),
            AgentDisplayName = context.AgentProfile.DisplayName,
            Language = context.AgentProfile.Language,
            ModelName = EffectiveModelName(context),
            PromptOverride = context.AgentProfile.PromptOverride,
            WorkingHoursSummary = WorkingHoursPromptFormatter.Format(WorkingHoursSharedAdapter.ToShared(context.Company.WorkingHours)),
        });
    }

    private static LlmProvider EffectiveProvider(ProcessorContext context)
    {
        return context.Conversation.LlmProvider ?? context.AgentProfile.LlmProvider;
    }

    private static string EffectiveModelName(ProcessorContext context)
    {
        return string.IsNullOrWhiteSpace(context.Conversation.ModelName)
            ? context.AgentProfile.ModelName
            : context.Conversation.ModelName;
    }

}
