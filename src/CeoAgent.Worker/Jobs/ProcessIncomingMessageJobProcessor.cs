using CeoAgent.Application.Agents;
using CeoAgent.Application.Abstractions.Organization;
using CeoAgent.Infrastructure;
using CeoAgent.Infrastructure.Entities;
using CeoAgent.Infrastructure.Entities.JsonDocuments;
using CeoAgent.Infrastructure.Persistence.Extensions;
using CeoAgent.Application.Abstractions.AI;
using CeoAgent.Shared.AI;
using CeoAgent.Application.Abstractions.Jobs;
using CeoAgent.Shared.Jobs;
using CeoAgent.Application.Abstractions.Messaging;
using CeoAgent.Shared.Messaging;
using CeoAgent.Shared.Enums;
using CeoAgent.Shared.Prompt;
using CeoAgent.Infrastructure.Implementation.AITools.Execution;
using CeoAgent.Infrastructure.Implementation.AITools.Handoff;
using CeoAgent.Shared.AITools;
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
    CompanyToolRegistry toolRegistry,
    ToolExecutionGateway toolGateway,
    HumanHandoffToolExecutor handoffExecutor,
    ReservationPaymentInstructionSender paymentInstructionSender,
    IOrganizationContextAccessor companyContextAccessor,
    TimeProvider timeProvider,
    IHostEnvironment hostEnvironment,
    ILogger<ProcessIncomingMessageJobProcessor> logger)
{
    private const string WhatsAppProvider = "whatsapp_cloud";
    private const string UnsupportedMessageText = "Por ahora solo puedo procesar mensajes de texto.";
    private const int MaxToolLoopIterations = 4;
    private const string LoopCapFallbackText = "No pude completar la accion automatica. Te pondre en contacto con una persona del equipo.";

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
            var agentText = await RunAgentLoopAsync(context, prompt, cancellationToken);
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
            context.AgentProfile.LlmProvider.ToString(),
            context.AgentProfile.ModelName,
            context.Tools.Count);
    }

    private async Task<ProcessorContext> LoadContextAsync(ProcessIncomingMessageJob job, CancellationToken cancellationToken)
    {
        var company = await dbContext.Companies.AsNoTracking().SingleOrDefaultAsync(entity => entity.Id == job.OrganizationId, cancellationToken)
            ?? throw new InvalidOperationException($"Company '{job.OrganizationId}' was not found.");
        var conversation = await dbContext.Conversations.ForOrganization(job.OrganizationId).SingleAsync(entity => entity.Id == job.ConversationId, cancellationToken);

        var agentProfile = await dbContext.AgentProfiles.AsNoTracking().ForOrganization(job.OrganizationId).SingleAsync(entity => entity.Id == conversation.AgentProfileId, cancellationToken);
        var channel = await dbContext.CompanyChannels.AsNoTracking().ForOrganization(job.OrganizationId).SingleOrDefaultAsync(entity => entity.Id == conversation.CompanyChannelId, cancellationToken)
            ?? throw new InvalidOperationException($"Company channel '{conversation.CompanyChannelId}' was not found.");

        var customer = await dbContext.Customers
            .AsNoTracking()
            .ForOrganization(job.OrganizationId)
            .SingleOrDefaultAsync(
                entity => entity.Id == conversation.CustomerId,
                cancellationToken)
            ?? throw new InvalidOperationException($"Customer '{conversation.CustomerId}' was not found.");

        var inbound = await dbContext.Messages
            .AsNoTracking()
            .ForConversation(job.OrganizationId, job.ConversationId)
            .SingleOrDefaultAsync(
                entity => entity.Id == job.MessageId,
                cancellationToken)
            ?? throw new InvalidOperationException($"Message '{job.MessageId}' was not found.");

        var messageHistory = await dbContext.Messages
            .AsNoTracking()
            .AgentEligibleHistory(dbContext.ToolExecutions, job.OrganizationId, job.ConversationId, 8)
            .Select(entity => new MessageHistoryItem(entity.Role, entity.MessageText))
            .ToArrayAsync(cancellationToken);

        Array.Reverse(messageHistory);

        var tools = await toolRegistry.GetEnabledToolsAsync(job.OrganizationId, cancellationToken);

        return new ProcessorContext(
            company,
            agentProfile,
            conversation,
            channel,
            customer,
            inbound,
            messageHistory.Select(entity => entity.ToAgentMessage()).ToArray(),
            tools);
    }

    private async Task<string?> RunAgentLoopAsync(
        ProcessorContext context,
        string prompt,
        CancellationToken cancellationToken)
    {
        var messages = context.Messages.ToList();
        var telemetryContext = CreateTelemetryContext(context);
        const bool sideEffectsEnabled = true;
        var maxEstimatedCostUsd = context.AgentProfile.MaxEstimatedCostUsdPerJob;
        var accumulatedEstimatedCostUsd = 0d;

        if (maxEstimatedCostUsd > 0
            && !IsLocalDevelopmentOrTesting()
            && !agentRuntime.CanEstimateCost(context.AgentProfile.LlmProvider, context.AgentProfile.ModelName))
        {
            ProcessIncomingMessageJobTelemetry.RecordLlmCostEstimationMissing();
            LlmCostPricingMissing(
                logger,
                context.Company.Id,
                context.AgentProfile.Id,
                context.AgentProfile.ModelName,
                hostEnvironment.EnvironmentName);
            await EscalateToHumanAsync(context, cancellationToken);
            return LoopCapFallbackText;
        }

        for (var iteration = 0; iteration < MaxToolLoopIterations; iteration++)
        {
            if (IsLlmBudgetExceeded(maxEstimatedCostUsd, accumulatedEstimatedCostUsd))
            {
                ProcessIncomingMessageJobTelemetry.RecordLlmBudgetExceeded();
                LlmBudgetExceeded(
                    logger,
                    context.Company.Id,
                    context.AgentProfile.Id,
                    context.AgentProfile.ModelName,
                    accumulatedEstimatedCostUsd,
                    maxEstimatedCostUsd);
                await EscalateToHumanAsync(context, cancellationToken);
                return LoopCapFallbackText;
            }

            AgentRunResult agentResult;
            using var iterationActivity = ProcessIncomingMessageJobTelemetry.StartAgentIteration(telemetryContext, iteration);
            using (var activity = ProcessIncomingMessageJobTelemetry.StartLlmGeneration(telemetryContext, iteration))
            {
                var stopwatch = Stopwatch.StartNew();
                try
                {
                    agentResult = await agentRuntime.RunAsync(
                        new AgentRunRequest(
                            context.AgentProfile.LlmProvider,
                            context.AgentProfile.ModelName,
                            prompt,
                            [.. messages],
                            context.Tools,
                            context.AgentProfile.MaxOutputTokenCount),
                        cancellationToken);
                    stopwatch.Stop();
                    ProcessIncomingMessageJobTelemetry.RecordLlmDuration(stopwatch.Elapsed);
                    ProcessIncomingMessageJobTelemetry.RecordTokenUsage(agentResult);
                    if (agentResult.EstimatedCostUsd is { } estimatedCost)
                    {
                        accumulatedEstimatedCostUsd += estimatedCost;
                    }
                    else if (maxEstimatedCostUsd > 0)
                    {
                        ProcessIncomingMessageJobTelemetry.RecordLlmCostEstimationMissing();
                    }

                    ProcessIncomingMessageJobTelemetry.EnrichLlmGenerationResult(
                        activity,
                        agentResult,
                        context.AgentProfile.ModelName);
                    ProcessIncomingMessageJobTelemetry.MarkOk(activity);
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
                        iteration);
                    return LoopCapFallbackText;
                }
            }

            if (IsLlmBudgetExceeded(maxEstimatedCostUsd, accumulatedEstimatedCostUsd))
            {
                ProcessIncomingMessageJobTelemetry.RecordLlmBudgetExceeded();
                LlmBudgetExceeded(
                    logger,
                    context.Company.Id,
                    context.AgentProfile.Id,
                    context.AgentProfile.ModelName,
                    accumulatedEstimatedCostUsd,
                    maxEstimatedCostUsd);
                await EscalateToHumanAsync(context, cancellationToken);
                return LoopCapFallbackText;
            }

            if (agentResult.ToolCalls.Count == 0)
            {
                ProcessIncomingMessageJobTelemetry.MarkOk(iterationActivity);
                return agentResult.AssistantText;
            }

            foreach (var toolCall in agentResult.ToolCalls)
            {
                var triggerMessage = new Message
                {
                    OrganizationId = context.Company.Id,
                    ConversationId = context.Conversation.Id,
                    Role = MessageRole.ToolCall,
                    Type = MessageType.Text,
                    MessageText = toolCall.Name,
                    OccurredAt = timeProvider.GetUtcNow().UtcDateTime,
                };
                dbContext.Messages.Add(triggerMessage);
                ToolCallRequested(
                    logger,
                    toolCall.Name,
                    iteration,
                    sideEffectsEnabled);

                messages.Add(new AgentConversationMessage(
                    "assistant",
                    toolCall.Name,
                    toolCall.Id,
                    toolCall.Name,
                    toolCall.Arguments));

                var toolResult = await toolGateway.ExecuteAsync(
                    new ToolExecutionGatewayRequest(
                        context.Company.Id,
                        context.Conversation.Id,
                        triggerMessage.Id,
                        context.Inbound.Id,
                        toolCall,
                        context.Tools,
                        sideEffectsEnabled),
                    cancellationToken);

                messages.Add(new AgentConversationMessage(
                    "tool",
                    toolResult.Content,
                    toolResult.ToolCallId,
                    toolResult.ToolName));
            }

            ProcessIncomingMessageJobTelemetry.MarkOk(iterationActivity);
        }

        AgentLoopCapReached(
            logger,
            MaxToolLoopIterations);
        await EscalateToHumanAsync(context, cancellationToken);
        return LoopCapFallbackText;
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
        Message Inbound,
        IReadOnlyList<AgentConversationMessage> Messages,
        IReadOnlyList<AgentToolDescriptor> Tools);

    private sealed record MessageHistoryItem(MessageRole Role, string? MessageText)
    {
        /// <summary>
        /// Converts persisted message history into the role/text shape consumed by the agent runtime.
        /// </summary>
        public AgentConversationMessage ToAgentMessage()
        {
            return new AgentConversationMessage(Role.ToString(), MessageText);
        }
    }

    private string BuildPrompt(ProcessorContext context)
    {
        return AgentPromptBuilder.Build(new AgentPromptContext
        {
            CompanyName = context.Company.Name,
            TimeZoneId = context.Company.TimeZoneId,
            LocalNow = ToCompanyLocalNow(context.Company.TimeZoneId),
            AgentDisplayName = context.AgentProfile.DisplayName,
            Language = context.AgentProfile.Language,
            ModelName = context.AgentProfile.ModelName,
            PromptOverride = context.AgentProfile.PromptOverride,
            WorkingHoursSummary = WorkingHoursPromptFormatter.Format(WorkingHoursSharedAdapter.ToShared(context.Company.WorkingHours)),
            Tools = context.Tools,
        });
    }

}
