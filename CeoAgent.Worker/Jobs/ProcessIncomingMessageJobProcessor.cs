using CeoAgent.Application.Agents;
using CeoAgent.Application.Company;
using CeoAgent.Infrastructure;
using CeoAgent.Infrastructure.Entities;
using CeoAgent.Infrastructure.Entities.JsonDocuments;
using CeoAgent.Integrations.Jobs;
using CeoAgent.Integrations.Messaging;
using CeoAgent.Integrations.Speech;
using CeoAgent.Shared.Constants;
using CeoAgent.Shared.Enums;
using CeoAgent.Shared.Media;
using CeoAgent.Shared.Prompt;
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
    ICompanyContextAccessor companyContextAccessor,
    TimeProvider timeProvider,
    ILogger<ProcessIncomingMessageJobProcessor> logger)
{
    private const string WhatsAppProvider = "whatsapp_cloud";
    private const string AudioAckText = "Recibí tu audio, lo estoy revisando.";
    private const string DefaultVoiceName = "default";

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
            if (await HasProcessedReplyAsync(context.Conversation.Id, replyClientMessageId, cancellationToken))
            {
                return;
            }

            if (!string.IsNullOrWhiteSpace(context.Inbound.ProviderMessageId))
            {
                await messaging.MarkMessageReadAsync(
                    new ChannelMessageReference(
                        context.Company.Id,
                        context.Channel.Id,
                        WhatsAppProvider,
                        context.Inbound.ProviderMessageId),
                    cancellationToken);
            }

            if (context.Inbound.Type == MessageType.Audio)
            {
                await ProcessInboundAudioAsync(context, cancellationToken);
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

            var messages = context.Messages
                .ToArray();

            var agentResult = await agentRuntime.RunAsync(
                new AgentRunRequest(
                    context.AgentProfile.ModelName,
                    prompt,
                    messages,
                    context.Tools),
                cancellationToken);

            var assistantText = string.IsNullOrWhiteSpace(agentResult.AssistantText)
                ? string.Empty
                : agentResult.AssistantText.Trim();

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
            context.Conversation.LastMessageAt = timeProvider.GetUtcNow().UtcDateTime;
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        finally
        {
            companyContextAccessor.Clear();
        }
    }

    private async Task ProcessInboundAudioAsync(ProcessorContext context, CancellationToken cancellationToken)
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

    private async Task<bool> HasProcessedReplyAsync(
        Guid conversationId,
        string replyClientMessageId,
        CancellationToken cancellationToken)
    {
        return await dbContext.Messages.AnyAsync(
            message => message.ConversationId == conversationId
                && message.Role == MessageRole.Assistant
                && message.ProviderMessageId == replyClientMessageId,
            cancellationToken);
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
        var agentProfile = await dbContext.AgentProfiles
            .SingleAsync(entity => entity.CompanyId == job.CompanyId, cancellationToken);
        var conversation = await dbContext.Conversations
            .SingleAsync(
                entity => entity.Id == job.ConversationId
                    && entity.CompanyId == job.CompanyId,
                cancellationToken);
        var channel = await dbContext.CompanyChannels.FindAsync([conversation.CompanyChannelId], cancellationToken)
            ?? throw new InvalidOperationException($"Company channel '{conversation.CompanyChannelId}' was not found.");
        var customer = await dbContext.Customers.FindAsync([conversation.CustomerId], cancellationToken)
            ?? throw new InvalidOperationException($"Customer '{conversation.CustomerId}' was not found.");

        var inbound = await dbContext.Messages.FindAsync([job.MessageId], cancellationToken)
            ?? throw new InvalidOperationException($"Message '{job.MessageId}' was not found.");

        var messageHistory = await dbContext.Messages
            .Where(entity => entity.ConversationId == job.ConversationId)
            .OrderByDescending(entity => entity.OccurredAt)
            .Take(8)
            .Select(entity => new MessageHistoryItem(entity.Role, entity.MessageText))
            .ToArrayAsync(cancellationToken);

        Array.Reverse(messageHistory);

        var toolKeys = await dbContext.CompanyTools
            .Where(entity => entity.CompanyId == job.CompanyId && entity.IsEnabled)
            .OrderBy(entity => entity.ToolKey)
            .Select(entity => entity.ToolKey)
            .ToArrayAsync(cancellationToken);

        return new ProcessorContext(
            company,
            agentProfile,
            conversation,
            channel,
            customer,
            inbound,
            messageHistory.Select(entity => entity.ToAgentMessage()).ToArray(),
            toolKeys.Select(ToDescriptor).ToArray());
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
                .Select(holiday => new CeoAgent.Shared.JsonDocuments.SpecialDay
                {
                    Date = holiday.Date,
                    IsClosed = holiday.IsClosed,
                    Reason = holiday.Reason,
                    TimeSlots = ConvertSlots(holiday.TimeSlots),
                })
                .ToList(),
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

    private static EnabledToolDescriptor ToDescriptor(string toolKey)
    {
        var description = toolKey switch
        {
            MvpToolKeys.CheckGoogleCalendarAvailability => "Check Google Calendar availability before offering or confirming reservation times.",
            MvpToolKeys.CreateGoogleCalendarReservation => "Create a Google Calendar reservation after explicit customer confirmation.",
            MvpToolKeys.RequestHumanHandoff => "Request a human handoff for this conversation.",
            _ => "Company-enabled tool.",
        };

        return new EnabledToolDescriptor(toolKey, description);
    }

    private sealed record ProcessorContext(
        Company Company,
        AgentProfile AgentProfile,
        Conversation Conversation,
        CompanyChannel Channel,
        Customer Customer,
        Message Inbound,
        IReadOnlyList<AgentConversationMessage> Messages,
        IReadOnlyList<EnabledToolDescriptor> Tools);

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
