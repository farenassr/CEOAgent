using CeoAgent.Application;
using CeoAgent.Application.Agents;
using CeoAgent.Application.Company.Abstractions;
using CeoAgent.Application.Company.Implementation;
using CeoAgent.Infrastructure;
using CeoAgent.Infrastructure.Entities;
using CeoAgent.Infrastructure.Entities.JsonDocuments;
using CeoAgent.Integrations.AI;
using CeoAgent.Integrations.Jobs;
using CeoAgent.Integrations.Messaging;
using CeoAgent.Integrations.Speech;
using CeoAgent.Shared.Constants;
using CeoAgent.Shared.Enums;
using CeoAgent.Shared.Media;
using CeoAgent.Shared.Prompt;
using CeoAgent.Tools.Implementation.Execution;
using CeoAgent.Tools.Models.Execution;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;
using ZLogger;

namespace CeoAgent.Worker.Jobs;

/// <summary>
/// Processes an inbound message job by loading conversation context, invoking the agent, and sending the reply.
/// </summary>
public sealed class ProcessIncomingMessageJobProcessor(
    CeoAgentDbContext dbContext,
    IMessageChannelIntegration messaging,
    ITranscriptionIntegration transcription,
    ISpeechSynthesisIntegration speechSynthesis,
    IAudioBlobStore audioBlobStore,
    IAgentRuntime agentRuntime,
    CompanyToolRegistry toolRegistry,
    ToolExecutionGateway toolGateway,
    ICompanyContextAccessor companyContextAccessor,
    TimeProvider timeProvider,
    ILogger<ProcessIncomingMessageJobProcessor> logger)
{
    private const string WhatsAppProvider = "whatsapp_cloud";
    private const string AudioAckText = "Recibí tu audio, lo estoy revisando.";
    private const string DefaultVoiceName = "default";
    private const int MaxToolLoopIterations = 4;
    private const string LoopCapFallbackText = "No pude completar la accion automatica. Te pondre en contacto con una persona del equipo.";

    /// <summary>
    /// Runs the inbound message workflow, including read receipts, audio transcription, prompt creation, and outbound response delivery.
    /// </summary>
    public async Task ProcessAsync(ProcessIncomingMessageJob job, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(job);

        using var logScope = BeginJobScope(job);
        companyContextAccessor.SetCompany(job.CompanyId);
        try
        {
            var context = await LoadContextAsync(job, cancellationToken);
            var replyClientMessageId = ReplyClientMessageId(context.Inbound);

            var existingReply = await dbContext.Messages
                .IgnoreQueryFilters()
                .Where(m => m.ConversationId == context.Conversation.Id
                    && m.Role == MessageRole.Assistant
                    && m.ProviderMessageId == replyClientMessageId)
                .SingleOrDefaultAsync(cancellationToken);

            if (existingReply is not null)
            {
                if (!string.IsNullOrEmpty(existingReply.Payload?.ProviderMessageId))
                {
                    return;
                }

                await SendExistingReplyAsync(context, existingReply, replyClientMessageId, cancellationToken);
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

            if (context.Inbound.Type == MessageType.Audio)
            {
                bool sttSuccess = await ProcessInboundAudioAsync(context, cancellationToken);
                if (!sttSuccess)
                {
                    var fallbackText = "No pude procesar tu audio. Un agente humano se comunicará contigo pronto.";
                    var assistantMessage = new Message
                    {
                        CompanyId = context.Company.Id,
                        ConversationId = context.Conversation.Id,
                        Role = MessageRole.Assistant,
                        Type = MessageType.Text,
                        MessageText = fallbackText,
                        ProviderMessageId = replyClientMessageId,
                        Payload = new MessagePayload
                        {
                            ProviderType = "text",
                        },
                        OccurredAt = timeProvider.GetUtcNow().UtcDateTime,
                    };

                    dbContext.Messages.Add(assistantMessage);

                    var sentMessage = await messaging.SendTextAsync(
                        new ChannelTextMessage(
                            context.Company.Id,
                            context.Channel.Id,
                            context.Conversation.Id,
                            assistantMessage.Id,
                            context.Customer.ExternalCustomerId,
                            fallbackText,
                            replyClientMessageId),
                        cancellationToken);

                    MarkAssistantSent(assistantMessage, sentMessage);
                    context.Conversation.LastMessageAt = timeProvider.GetUtcNow().UtcDateTime;
                    await SaveFinalStateAsync(job.CompanyId, job.ConversationId, job.JobId, cancellationToken);
                    return;
                }
            }

            var prompt = AgentPromptBuilder.Build(new AgentPromptContext
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

            var agentText = await RunAgentLoopAsync(context, prompt, cancellationToken);
            var assistantText = string.IsNullOrWhiteSpace(agentText)
                ? string.Empty
                : agentText.Trim();

            var assistant = new Message
            {
                CompanyId = context.Company.Id,
                ConversationId = context.Conversation.Id,
                Role = MessageRole.Assistant,
                Type = context.Inbound.Type == MessageType.Audio ? MessageType.Audio : MessageType.Text,
                MessageText = assistantText,
                ProviderMessageId = replyClientMessageId,
                Payload = new MessagePayload
                {
                    ProviderType = context.Inbound.Type == MessageType.Audio ? "audio" : "text",
                },
                OccurredAt = timeProvider.GetUtcNow().UtcDateTime,
            };

            dbContext.Messages.Add(assistant);
            context.Conversation.LastMessageAt = timeProvider.GetUtcNow().UtcDateTime;

            SentMessageReference sent;
            if (context.Inbound.Type == MessageType.Audio)
            {
                sent = await SendAudioReplyAsync(context, assistant, assistantText, replyClientMessageId, cancellationToken);
            }
            else
            {
                sent = await messaging.SendTextAsync(
                    new ChannelTextMessage(
                        context.Company.Id,
                        context.Channel.Id,
                        context.Conversation.Id,
                        assistant.Id,
                        context.Customer.ExternalCustomerId,
                        assistantText,
                        replyClientMessageId),
                    cancellationToken);
            }

            MarkAssistantSent(assistant, sent);
            await SaveFinalStateAsync(job.CompanyId, job.ConversationId, job.JobId, cancellationToken);
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

    private async Task<bool> ProcessInboundAudioAsync(ProcessorContext context, CancellationToken cancellationToken)
    {
        try
        {
            await messaging.SendTextAsync(
                new ChannelTextMessage(
                    context.Company.Id,
                    context.Channel.Id,
                    context.Conversation.Id,
                    Guid.CreateVersion7(),
                    context.Customer.ExternalCustomerId,
                    AudioAckText,
                    AudioAckClientMessageId(context.Inbound)),
                cancellationToken);

            var audioPayload = context.Inbound.Payload?.Audio
                ?? throw new InvalidOperationException("Inbound audio message requires audio payload.");

            var providerMediaId = audioPayload.ProviderMediaId ?? audioPayload.BlobUri;
            audioPayload.SttStatus = SpeechProcessingStatus.Processing;

            var media = await messaging.DownloadMediaAsync(
                new ChannelMediaReference(
                    context.Company.Id,
                    context.Channel.Id,
                    WhatsAppProvider,
                    providerMediaId),
                cancellationToken);
            await using var mediaContent = media.Content;

            var blobPath = AudioBlobNaming.CreatePath(
                context.Company.Name,
                context.Company.Id,
                AudioBlobDirection.Inbound,
                timeProvider.GetUtcNow(),
                context.Inbound.Id,
                media.OriginalExtension);

            var sizeBytes = media.SizeBytes
                ?? (mediaContent.CanSeek
                    ? mediaContent.Length
                    : throw new InvalidOperationException("Inbound audio stream size is required when the stream cannot be measured."));
            var stored = await audioBlobStore.StoreAsync(
                new AudioBlobStoreRequest(
                    blobPath,
                    mediaContent,
                    media.ContentType,
                    sizeBytes,
                    AudioBlobDirection.Inbound),
                cancellationToken);

            audioPayload.BlobUri = stored.BlobUri.ToString();
            audioPayload.ContentType = media.ContentType;
            audioPayload.SizeBytes = stored.SizeBytes;

            if (!mediaContent.CanSeek)
            {
                throw new InvalidOperationException("Inbound audio stream must be seekable or buffered once before reuse.");
            }

            mediaContent.Position = 0;

            var transcript = await transcription.TranscribeAsync(
                new TranscriptionRequest(
                    mediaContent,
                    media.ContentType,
                    audioPayload.Language,
                    context.AgentProfile.ModelName),
                cancellationToken);

            context.Inbound.MessageText = transcript.Text;
            audioPayload.Language = transcript.Language ?? audioPayload.Language;
            audioPayload.DurationMs = transcript.Duration is null ? audioPayload.DurationMs : (int)transcript.Duration.Value.TotalMilliseconds;
            audioPayload.SttStatus = SpeechProcessingStatus.Completed;
            return true;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.ZLogError(ex, $"InboundAudioSttFailed company_id={context.Company.Id} conversation_id={context.Conversation.Id} message_id={context.Inbound.Id}");

            if (context.Inbound.Payload?.Audio is { } audioPayload)
            {
                audioPayload.SttStatus = SpeechProcessingStatus.Failed;
            }

            context.Conversation.Status = ConversationStatus.HandedOff;
            return false;
        }
    }

    private async Task<SentMessageReference> SendAudioReplyAsync(
        ProcessorContext context,
        Message assistant,
        string assistantText,
        string replyClientMessageId,
        CancellationToken cancellationToken)
    {
        try
        {
            var synthesized = await speechSynthesis.SynthesizeAsync(
                new SpeechSynthesisRequest(
                    assistantText,
                    context.AgentProfile.Language,
                    DefaultVoiceName,
                    context.AgentProfile.ModelName),
                cancellationToken);

            var blobPath = AudioBlobNaming.CreatePath(
                context.Company.Name,
                context.Company.Id,
                AudioBlobDirection.Outbound,
                timeProvider.GetUtcNow(),
                assistant.Id,
                synthesized.Extension);

            var sizeBytes = synthesized.Audio.Length;
            var stored = await audioBlobStore.StoreAsync(
                new AudioBlobStoreRequest(
                    blobPath,
                    synthesized.Audio,
                    synthesized.ContentType,
                    sizeBytes,
                    AudioBlobDirection.Outbound),
                cancellationToken);

            assistant.Payload = MessagePayload.ForAudio(
                "audio",
                new AudioPayload
                {
                    BlobUri = stored.BlobUri.ToString(),
                    ContentType = synthesized.ContentType,
                    SizeBytes = stored.SizeBytes,
                    Language = context.AgentProfile.Language,
                    TtsStatus = SpeechProcessingStatus.Completed,
                });

            return await messaging.SendAudioAsync(
                new ChannelAudioMessage(
                    context.Company.Id,
                    context.Channel.Id,
                    context.Conversation.Id,
                    assistant.Id,
                    context.Customer.ExternalCustomerId,
                    stored.BlobUri,
                    synthesized.ContentType,
                    replyClientMessageId),
                cancellationToken);
        }
        catch (Exception exception)
        {
            logger.ZLogError(
                exception,
                $"SpeechSynthesisFailed company_id={context.Company.Id} company_channel_id={context.Channel.Id} conversation_id={context.Conversation.Id} message_id={assistant.Id} inbound_message_id={context.Inbound.Id} language={context.AgentProfile.Language} model_name={context.AgentProfile.ModelName} text_length={assistantText.Length}");

            assistant.Type = MessageType.Text;
            assistant.Payload = MessagePayload.ForAudio(
                "audio",
                new AudioPayload
                {
                    BlobUri = string.Empty,
                    ContentType = "application/octet-stream",
                    SizeBytes = 0,
                    Language = context.AgentProfile.Language,
                    TtsStatus = SpeechProcessingStatus.Failed,
                });

            return await messaging.SendTextAsync(
                new ChannelTextMessage(
                    context.Company.Id,
                    context.Channel.Id,
                    context.Conversation.Id,
                    assistant.Id,
                    context.Customer.ExternalCustomerId,
                    assistant.MessageText ?? string.Empty,
                    replyClientMessageId),
                cancellationToken);
        }
    }

    private async Task SendExistingReplyAsync(
        ProcessorContext context,
        Message existingReply,
        string replyClientMessageId,
        CancellationToken cancellationToken)
    {
        SentMessageReference sent;
        if (context.Inbound.Type == MessageType.Audio)
        {
            sent = await SendAudioReplyAsync(context, existingReply, existingReply.MessageText ?? string.Empty, replyClientMessageId, cancellationToken);
        }
        else
        {
            sent = await messaging.SendTextAsync(
                new ChannelTextMessage(
                    context.Company.Id,
                    context.Channel.Id,
                    context.Conversation.Id,
                    existingReply.Id,
                    context.Customer.ExternalCustomerId,
                    existingReply.MessageText ?? string.Empty,
                    replyClientMessageId),
                cancellationToken);
        }

        MarkAssistantSent(existingReply, sent);
        context.Conversation.LastMessageAt = timeProvider.GetUtcNow().UtcDateTime;
        await SaveFinalStateAsync(context.Company.Id, context.Conversation.Id, null, cancellationToken);
    }

    private async Task SaveFinalStateAsync(
        Guid companyId,
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
            logger.ZLogWarning($"ConversationConcurrencyConflict company_id={companyId} conversation_id={conversationId} job_id={jobId}");
            throw;
        }
    }

    private IDisposable? BeginJobScope(ProcessIncomingMessageJob job)
    {
        return logger.BeginScope(new Dictionary<string, object?>
        {
            ["correlation_id"] = job.CorrelationId,
            ["company_id"] = job.CompanyId,
            ["conversation_id"] = job.ConversationId,
            ["job_id"] = job.JobId,
            ["trace_id"] = Activity.Current?.TraceId.ToString(),
        });
    }

    private static string ReplyClientMessageId(Message inbound)
    {
        return $"reply:{inbound.Id}";
    }

    private static string AudioAckClientMessageId(Message inbound)
    {
        return $"audio-ack:{inbound.Id}";
    }

    private static void MarkAssistantSent(Message assistant, SentMessageReference sent)
    {
        assistant.Payload ??= new MessagePayload();
        assistant.Payload.ProviderMessageId = sent.ProviderMessageId;
    }

    private async Task<ProcessorContext> LoadContextAsync(ProcessIncomingMessageJob job, CancellationToken cancellationToken)
    {
        var company = await dbContext.Companies.FindAsync([job.CompanyId], cancellationToken)
            ?? throw new InvalidOperationException($"Company '{job.CompanyId}' was not found.");
        var conversation = await dbContext.Conversations
            .SingleAsync(
                entity => entity.Id == job.ConversationId
                    && entity.CompanyId == job.CompanyId,
                cancellationToken);
        var agentProfile = await dbContext.AgentProfiles
            .SingleAsync(
                entity => entity.Id == conversation.AgentProfileId
                    && entity.CompanyId == job.CompanyId,
                cancellationToken);
        var channel = await dbContext.CompanyChannels
            .SingleOrDefaultAsync(
                entity => entity.Id == conversation.CompanyChannelId
                    && entity.CompanyId == job.CompanyId,
                cancellationToken)
            ?? throw new InvalidOperationException($"Company channel '{conversation.CompanyChannelId}' was not found.");
        var customer = await dbContext.Customers
            .SingleOrDefaultAsync(
                entity => entity.Id == conversation.CustomerId
                    && entity.CompanyId == job.CompanyId,
                cancellationToken)
            ?? throw new InvalidOperationException($"Customer '{conversation.CustomerId}' was not found.");

        var inbound = await dbContext.Messages
            .SingleOrDefaultAsync(
                entity => entity.Id == job.MessageId
                    && entity.CompanyId == job.CompanyId
                    && entity.ConversationId == job.ConversationId,
                cancellationToken)
            ?? throw new InvalidOperationException($"Message '{job.MessageId}' was not found.");

        var messageHistory = await dbContext.Messages
            .Where(entity => entity.ConversationId == job.ConversationId)
            .Where(entity =>
                entity.Role == MessageRole.User
                || (entity.Role == MessageRole.Assistant
                    && !dbContext.ToolExecutions.Any(execution => execution.TriggerMessageId == entity.Id)))
            .OrderByDescending(entity => entity.OccurredAt)
            .ThenByDescending(entity => entity.Id)
            .Take(8)
            .Select(entity => new MessageHistoryItem(entity.Role, entity.MessageText))
            .ToArrayAsync(cancellationToken);

        Array.Reverse(messageHistory);

        var tools = await toolRegistry.GetEnabledToolsAsync(job.CompanyId, cancellationToken);

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
            activity?.SetTag("company.id", context.Company.Id);
            activity?.SetTag("conversation.id", context.Conversation.Id);
            activity?.SetTag("channel", context.Channel.Provider.ToString());
            activity?.SetTag("llm.provider", context.AgentProfile.LlmProvider.ToString());
            activity?.SetTag("llm.model", context.AgentProfile.ModelName);
            activity?.SetTag("agent.iteration", iteration);
            activity?.SetTag("tool.count", context.Tools.Count);
            var stopwatch = Stopwatch.StartNew();
            try
            {
                agentResult = await agentRuntime.RunAsync(
                    new AgentRunRequest(
                        context.AgentProfile.LlmProvider,
                        context.AgentProfile.ModelName,
                        prompt,
                        messages.ToArray(),
                        context.Tools),
                    cancellationToken);
                stopwatch.Stop();
                CeoAgentTelemetry.LlmCallDuration.Record(stopwatch.ElapsedMilliseconds);
                activity?.SetTag("llm.response.id", agentResult.ResponseId);
                activity?.SetTag("llm.finish_reason", agentResult.FinishReason);
                activity?.SetTag("llm.tool_call_count", agentResult.ToolCalls.Count);
                activity?.SetStatus(ActivityStatusCode.Ok);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                stopwatch.Stop();
                CeoAgentTelemetry.LlmCallDuration.Record(stopwatch.ElapsedMilliseconds);
                activity?.SetStatus(ActivityStatusCode.Error, exception.GetType().Name);
                logger.ZLogError(
                    exception,
                    $"AgentRuntimeFailed conversation_id={context.Conversation.Id} company_id={context.Company.Id} iteration={iteration}");
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
                    CompanyId = context.Company.Id,
                    ConversationId = context.Conversation.Id,
                    Role = MessageRole.ToolCall,
                    Type = MessageType.Text,
                    MessageText = toolCall.Name,
                    OccurredAt = timeProvider.GetUtcNow().UtcDateTime,
                };
                dbContext.Messages.Add(triggerMessage);
                logger.ZLogInformation(
                    $"ToolCallRequested tool={toolCall.Name} iteration={iteration} conversation_id={context.Conversation.Id} company_id={context.Company.Id} side_effects_enabled={sideEffectsEnabled}");

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

        logger.ZLogWarning(
            $"AgentLoopCapReached conversation_id={context.Conversation.Id} company_id={context.Company.Id} max_iterations={MaxToolLoopIterations}");
        return LoopCapFallbackText;
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
}
