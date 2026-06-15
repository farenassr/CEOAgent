using CeoAgent.Application;
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
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;
using System.Text.Json;

namespace CeoAgent.Worker.Jobs;

/// <summary>
/// Processes an inbound message job by loading conversation context, invoking the agent, and sending the reply.
/// </summary>
public sealed partial class ProcessIncomingMessageJobProcessor(
    CeoAgentDbContext dbContext,
    IMessageChannelIntegration messaging,
    IAgentRuntime agentRuntime,
    CompanyToolRegistry toolRegistry,
    ToolExecutionGateway toolGateway,
    HumanHandoffToolExecutor handoffExecutor,
    ReservationPaymentInstructionSender paymentInstructionSender,
    IOrganizationContextAccessor companyContextAccessor,
    TimeProvider timeProvider,
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

        using var logScope = BeginJobScope(job);
        companyContextAccessor.SetOrganization(job.OrganizationId);

        try
        {
            var context = await LoadContextAsync(job, cancellationToken);

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

                await SendExistingReplyAsync(context, existingReply, replyClientMessageId, cancellationToken);
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
            await SaveFinalStateAsync(job.OrganizationId, job.ConversationId, job.JobId, cancellationToken);

            var sent = await messaging.SendTextAsync(
                new ChannelTextMessage(
                    context.Company.Id,
                    context.Channel.Id,
                    context.Conversation.Id,
                    assistant.Id,
                    context.Customer.ExternalCustomerId,
                    assistantText,
                    replyClientMessageId),
                cancellationToken);

            MarkAssistantSent(assistant, sent);
            await SaveFinalStateAsync(job.OrganizationId, job.ConversationId, job.JobId, cancellationToken);
            await paymentInstructionSender.SendForSuccessfulReservationsAsync(
                context.Company.Id,
                context.Conversation.Id,
                context.Inbound.Id,
                context.Channel.Id,
                context.Customer.ExternalCustomerId,
                cancellationToken);
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
        await SaveFinalStateAsync(job.OrganizationId, job.ConversationId, job.JobId, cancellationToken);

        var sent = await messaging.SendTextAsync(
            new ChannelTextMessage(
                context.Company.Id,
                context.Channel.Id,
                context.Conversation.Id,
                assistant.Id,
                context.Customer.ExternalCustomerId,
                text,
                replyClientMessageId),
            cancellationToken);

        MarkAssistantSent(assistant, sent);
        await SaveFinalStateAsync(job.OrganizationId, job.ConversationId, job.JobId, cancellationToken);
    }

    private async Task SendExistingReplyAsync(
        ProcessorContext context,
        Message existingReply,
        string replyClientMessageId,
        CancellationToken cancellationToken)
    {
        var sent = await messaging.SendTextAsync(
            new ChannelTextMessage(
                context.Company.Id,
                context.Channel.Id,
                context.Conversation.Id,
                existingReply.Id,
                context.Customer.ExternalCustomerId,
                existingReply.MessageText ?? string.Empty,
                replyClientMessageId),
            cancellationToken);

        MarkAssistantSent(existingReply, sent);
        context.Conversation.LastMessageAt = timeProvider.GetUtcNow().UtcDateTime;
        await SaveFinalStateAsync(context.Company.Id, context.Conversation.Id, jobId: null, cancellationToken);
    }

    private async Task SaveFinalStateAsync(
        Guid organizationId,
        Guid conversationId,
        Guid? jobId,
        CancellationToken cancellationToken)
    {
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            ConversationConcurrencyConflict(logger, organizationId, conversationId, jobId);
            throw;
        }
    }

    private static string ReplyClientMessageId(Message inbound)
    {
        return $"reply:{inbound.Id}";
    }

    private static void MarkAssistantSent(Message assistant, SentMessageReference sent)
    {
        assistant.Payload ??= new MessagePayload();
        assistant.Payload.ProviderMessageId = sent.ProviderMessageId;
    }

    private static void RecordTokenTelemetry(AgentRunResult agentResult)
    {
        if (agentResult.TotalTokenCount is { } totalTokens)
        {
            CeoAgentTelemetry.LlmTokensConsumed.Add(totalTokens);
        }

        if (agentResult.EstimatedCostUsd is { } estimatedCost)
        {
            CeoAgentTelemetry.LlmEstimatedCost.Add(estimatedCost);
        }
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
            .ForConversation(job.OrganizationId, job.ConversationId)
            .SingleOrDefaultAsync(
                entity => entity.Id == job.MessageId,
                cancellationToken)
            ?? throw new InvalidOperationException($"Message '{job.MessageId}' was not found.");

        var messageHistory = await dbContext.Messages
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
        const bool sideEffectsEnabled = true;

        for (var iteration = 0; iteration < MaxToolLoopIterations; iteration++)
        {
            AgentRunResult agentResult;
            using var activity = CeoAgentTelemetry.ActivitySource.StartActivity("agent.run");
            activity?.SetTag("organization.id", context.Company.Id);
            activity?.SetTag("conversation.id", context.Conversation.Id);
            activity?.SetTag("channel", context.Channel.Provider.ToString());
            activity?.SetTag("llm.provider", context.AgentProfile.LlmProvider.ToString());
            activity?.SetTag("llm.model", context.AgentProfile.ModelName);
            activity?.SetTag("agent.iteration", iteration);
            activity?.SetTag("tool.count", context.Tools.Count);
            activity?.SetTag(CeoAgentTelemetry.Langfuse.ObservationType, "generation");
            activity?.SetTag(CeoAgentTelemetry.Langfuse.ObservationModelName, context.AgentProfile.ModelName);
            activity?.SetTag(CeoAgentTelemetry.Langfuse.MetadataProvider, context.AgentProfile.LlmProvider.ToString());
            activity?.SetTag(CeoAgentTelemetry.Langfuse.MetadataOrganizationId, context.Company.Id.ToString());
            activity?.SetTag(CeoAgentTelemetry.Langfuse.MetadataConversationId, context.Conversation.Id.ToString());
            activity?.SetTag(CeoAgentTelemetry.Langfuse.MetadataChannel, context.Channel.Provider.ToString());
            var stopwatch = Stopwatch.StartNew();
            try
            {
                agentResult = await agentRuntime.RunAsync(
                    new AgentRunRequest(
                        context.AgentProfile.LlmProvider,
                        context.AgentProfile.ModelName,
                        prompt,
                        [.. messages],
                        context.Tools),
                    cancellationToken);
                stopwatch.Stop();
                CeoAgentTelemetry.LlmCallDuration.Record(stopwatch.ElapsedMilliseconds);
                RecordTokenTelemetry(agentResult);
                activity?.SetTag("llm.response.id", agentResult.ResponseId);
                activity?.SetTag("llm.finish_reason", agentResult.FinishReason);
                activity?.SetTag("llm.tool_call_count", agentResult.ToolCalls.Count);
                SetLangfuseUsageTags(activity, agentResult);
                activity?.SetStatus(ActivityStatusCode.Ok);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                stopwatch.Stop();
                CeoAgentTelemetry.LlmCallDuration.Record(stopwatch.ElapsedMilliseconds);
                activity?.SetStatus(ActivityStatusCode.Error, exception.GetType().Name);
                AgentRuntimeFailed(
                    logger,
                    exception,
                    iteration);
                return LoopCapFallbackText;
            }

            if (agentResult.ToolCalls.Count == 0)
            {
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
        }

        AgentLoopCapReached(
            logger,
            MaxToolLoopIterations);
        await EscalateToHumanAsync(context, cancellationToken);
        return LoopCapFallbackText;
    }

    private static void SetLangfuseUsageTags(Activity? activity, AgentRunResult agentResult)
    {
        if (activity is null)
        {
            return;
        }

        var usage = new Dictionary<string, int>(capacity: 3);
        if (agentResult.InputTokenCount is { } inputTokens)
        {
            usage["input"] = inputTokens;
        }

        if (agentResult.OutputTokenCount is { } outputTokens)
        {
            usage["output"] = outputTokens;
        }

        if (agentResult.TotalTokenCount is { } totalTokens)
        {
            usage["total"] = totalTokens;
        }

        if (usage.Count > 0)
        {
            activity.SetTag(CeoAgentTelemetry.Langfuse.ObservationUsageDetails, JsonSerializer.Serialize(usage));
        }
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

    private static CeoAgent.Shared.JsonDocuments.WorkingHours? ToSharedWorkingHours(WorkingHours? workingHours)
    {
        if (workingHours is null)
        {
            return null;
        }

        return new CeoAgent.Shared.JsonDocuments.WorkingHours
        {
            Schedule = new CeoAgent.Shared.JsonDocuments.WeeklySchedule
            {
                Monday = ConvertSlots(workingHours.Schedule.Monday),
                Tuesday = ConvertSlots(workingHours.Schedule.Tuesday),
                Wednesday = ConvertSlots(workingHours.Schedule.Wednesday),
                Thursday = ConvertSlots(workingHours.Schedule.Thursday),
                Friday = ConvertSlots(workingHours.Schedule.Friday),
                Saturday = ConvertSlots(workingHours.Schedule.Saturday),
                Sunday = ConvertSlots(workingHours.Schedule.Sunday),
            },
            Holidays = workingHours.Holidays
                .ConvertAll(holiday => new CeoAgent.Shared.JsonDocuments.SpecialDay
                {
                    Date = holiday.Date,
                    IsClosed = holiday.IsClosed,
                    Reason = holiday.Reason,
                    TimeSlots = ConvertSlots(holiday.TimeSlots),
                })
,
        };
    }

    private static List<CeoAgent.Shared.JsonDocuments.TimeSlot> ConvertSlots(IEnumerable<TimeSlot> slots)
    {
        return slots
            .Select(slot => new CeoAgent.Shared.JsonDocuments.TimeSlot
            {
                Start = slot.Start,
                End = slot.End,
            })
            .ToList();
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
            WorkingHoursSummary = WorkingHoursPromptFormatter.Format(ToSharedWorkingHours(context.Company.WorkingHours)),
            Tools = context.Tools,
        });
    }

}
