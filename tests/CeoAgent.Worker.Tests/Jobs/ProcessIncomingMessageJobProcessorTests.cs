using System.Text;
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
using CeoAgent.Worker.Jobs;
using CeoAgent.Worker.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;

namespace CeoAgent.Worker.Tests.Jobs;

public sealed class ProcessIncomingMessageJobProcessorTests
{
    [Test]
    public async Task ProcessAsync_ForInboundAudio_TranscribesBuildsPromptAndRepliesWithTtsAudio()
    {
        await using var fixture = await ProcessorFixture.CreateAsync();
        fixture.Blobs.ConsumeStream = true;

        await fixture.Processor.ProcessAsync(
            new ProcessIncomingMessageJob(
                fixture.CompanyId,
                fixture.Conversation.Id,
                fixture.InboundMessage.Id,
                "correlation-123"),
            CancellationToken.None);

        fixture.Messaging.ReadMessages.Single().ProviderMessageId.ShouldBe("wamid.audio-1");
        fixture.Messaging.TextMessages.Single().Text.ShouldBe("Recibí tu audio, lo estoy revisando.");

        fixture.Blobs.Stored.Single(blob => blob.Direction == AudioBlobDirection.Inbound).Path
            .ShouldContain("/media/audio/inbound/");
        fixture.InboundMessage.MessageText.ShouldBe("Quiero reservar a las cuatro.");
        fixture.InboundMessage.Payload!.Audio!.SttStatus.ShouldBe(SpeechProcessingStatus.Completed);
        fixture.InboundMessage.Payload.Audio.BlobUri.ShouldContain("https://blob.test/");
        fixture.Transcription.LastRequestBytes.ShouldBeGreaterThan(0);

        fixture.Agent.Requests.Single().ModelName.ShouldBe("gpt-4.1-mini");
        fixture.Agent.Requests.Single().SystemPrompt.ShouldContain("Company: Contoso Bistro");
        fixture.Agent.Requests.Single().SystemPrompt.ShouldContain("Language: es");
        fixture.Agent.Requests.Single().SystemPrompt.ShouldContain("Responde corto.");
        fixture.Agent.Requests.Single().Tools.Select(tool => tool.Name).ShouldBe([
            MvpToolKeys.CheckGoogleCalendarAvailability,
            MvpToolKeys.CreateGoogleCalendarReservation,
        ]);

        fixture.Blobs.Stored.Single(blob => blob.Direction == AudioBlobDirection.Outbound).Path
            .ShouldContain("/media/audio/outbound/");
        fixture.Messaging.AudioMessages.Single().AudioUri.ToString().ShouldContain("https://blob.test/");

        var assistant = fixture.DbContext.ChangeTracker
            .Entries<Message>()
            .Select(entry => entry.Entity)
            .Single(message => message.Role == MessageRole.Assistant);
        assistant.MessageText.ShouldBe("Claro, reviso disponibilidad.");
        assistant.Payload!.Audio!.TtsStatus.ShouldBe(SpeechProcessingStatus.Completed);
    }

    [Test]
    public async Task ProcessAsync_WhenSameJobIsRetried_DoesNotSendDuplicateReply()
    {
        await using var fixture = await ProcessorFixture.CreateAsync();
        var job = new ProcessIncomingMessageJob(
            fixture.CompanyId,
            fixture.Conversation.Id,
            fixture.InboundMessage.Id,
            "correlation-123");

        await fixture.Processor.ProcessAsync(job, CancellationToken.None);
        await fixture.Processor.ProcessAsync(job, CancellationToken.None);

        fixture.Messaging.ReadMessages.Count.ShouldBe(1);
        fixture.Messaging.TextMessages.Count.ShouldBe(1);
        fixture.Messaging.AudioMessages.Count.ShouldBe(1);
        fixture.Agent.Requests.Count.ShouldBe(1);

        fixture.CompanyContext.SetCompany(fixture.CompanyId);
        var assistantCount = await fixture.DbContext.Messages
            .CountAsync(message => message.Role == MessageRole.Assistant);
        assistantCount.ShouldBe(1);
    }

    private sealed class ProcessorFixture
    {
        private readonly PostgresWorkerDatabase database;

        private ProcessorFixture(PostgresWorkerDatabase database)
        {
            this.database = database;
            CompanyContext = database.CompanyContext;
            CompanyContext.SetCompany(CompanyId);
            DbContext = database.Context;
            Messaging = new FakeMessageChannelIntegration();
            Transcription = new FakeTranscriptionIntegration();
            Speech = new FakeSpeechSynthesisIntegration();
            Blobs = new FakeAudioBlobStore();
            Agent = new FakeAgentRuntime();
            Processor = new ProcessIncomingMessageJobProcessor(
                DbContext,
                Messaging,
                Transcription,
                Speech,
                Blobs,
                Agent,
                CompanyContext,
                TimeProvider.System,
                NullLogger<ProcessIncomingMessageJobProcessor>.Instance);

            var company = new Company
            {
                Id = CompanyId,
                Name = "Contoso Bistro",
                TimeZoneId = "America/Bogota",
                WorkingHours = new WorkingHours
                {
                    Schedule = new WeeklySchedule
                    {
                        Thursday =
                        [
                            new TimeSlot
                            {
                                Start = new TimeOnly(12, 0),
                                End = new TimeOnly(22, 0),
                            },
                        ],
                    },
                },
            };

            var profile = new AgentProfile
            {
                Id = Guid.Parse("018f4f70-8b5f-7b4c-9d1a-0f6c1d7a2b32"),
                CompanyId = CompanyId,
                ModelName = "gpt-4.1-mini",
                DisplayName = "Contoso Assistant",
                Language = "es",
                PromptOverride = "Responde corto.",
            };

            Channel = CompanyChannel.ForWhatsAppCloud(
                CompanyId,
                "1152556904604978",
                new WhatsAppCloudMetadata
                {
                    BusinessAccountId = "840790722416204",
                    PhoneNumberId = "1152556904604978",
                    DisplayPhoneNumber = "+15556497030",
                },
                id: Guid.Parse("018f4f70-8b5f-7b4c-9d1a-0f6c1d7a2b31"));

            Customer = new Customer
            {
                Id = Guid.Parse("018f4f70-8b5f-7b4c-9d1a-0f6c1d7a2b33"),
                CompanyId = CompanyId,
                CompanyChannelId = Channel.Id,
                ExternalCustomerId = "15551234567",
            };

            Conversation = new Conversation
            {
                Id = Guid.Parse("018f4f70-8b5f-7b4c-9d1a-0f6c1d7a2b34"),
                CompanyId = CompanyId,
                CustomerId = Customer.Id,
                CompanyChannelId = Channel.Id,
                AgentProfileId = profile.Id,
                LastMessageAt = new DateTime(2026, 5, 28, 21, 0, 0, DateTimeKind.Utc),
            };

            InboundMessage = new Message
            {
                Id = Guid.Parse("018f4f70-8b5f-7b4c-9d1a-0f6c1d7a2b36"),
                CompanyId = CompanyId,
                ConversationId = Conversation.Id,
                Role = MessageRole.User,
                Type = MessageType.Audio,
                ProviderMessageId = "wamid.audio-1",
                Payload = MessagePayload.ForAudio(
                    "audio",
                    new AudioPayload
                    {
                        BlobUri = "whatsapp-media://audio-media-1",
                        ContentType = "audio/ogg",
                        SizeBytes = 0,
                        ProviderMediaId = "audio-media-1",
                        SttStatus = SpeechProcessingStatus.Pending,
                    },
                    "wamid.audio-1"),
                OccurredAt = new DateTime(2026, 5, 28, 21, 0, 0, DateTimeKind.Utc),
            };

            var checkTool = new CompanyTool
            {
                Id = Guid.Parse("018f4f70-8b5f-7b4c-9d1a-0f6c1d7a2b40"),
                CompanyId = CompanyId,
                ToolKey = MvpToolKeys.CheckGoogleCalendarAvailability,
                IsEnabled = true,
                Configuration = ToolConfiguration.ForGoogleCalendar(new GoogleCalendarConfig
                {
                    CalendarId = "primary",
                    TimeZoneId = "America/Bogota",
                    BufferMinutes = 0,
                }),
            };
            var createTool = new CompanyTool
            {
                Id = Guid.Parse("018f4f70-8b5f-7b4c-9d1a-0f6c1d7a2b41"),
                CompanyId = CompanyId,
                ToolKey = MvpToolKeys.CreateGoogleCalendarReservation,
                IsEnabled = true,
                Configuration = ToolConfiguration.ForGoogleCalendar(new GoogleCalendarConfig
                {
                    CalendarId = "primary",
                    TimeZoneId = "America/Bogota",
                    BufferMinutes = 0,
                }),
            };

            DbContext.AddRange(company, profile, Channel, Customer, Conversation, InboundMessage, checkTool, createTool);
            DbContext.SaveChanges();
        }

        public static async Task<ProcessorFixture> CreateAsync()
        {
            return new ProcessorFixture(await PostgresWorkerDatabase.CreateAsync());
        }

        public Guid CompanyId { get; } = Guid.Parse("018f4f70-8b5f-7b4c-9d1a-0f6c1d7a2b30");

        public CompanyContextAccessor CompanyContext { get; }

        public CeoAgentDbContext DbContext { get; }

        public FakeMessageChannelIntegration Messaging { get; }

        public FakeTranscriptionIntegration Transcription { get; }

        public FakeSpeechSynthesisIntegration Speech { get; }

        public FakeAudioBlobStore Blobs { get; }

        public FakeAgentRuntime Agent { get; }

        public ProcessIncomingMessageJobProcessor Processor { get; }

        public CompanyChannel Channel { get; }

        public Customer Customer { get; }

        public Conversation Conversation { get; }

        public Message InboundMessage { get; }

        public async ValueTask DisposeAsync()
        {
            await database.DisposeAsync();
        }
    }

    private sealed class FakeMessageChannelIntegration : IMessageChannelIntegration
    {
        public List<ChannelMessageReference> ReadMessages { get; } = [];

        public List<ChannelTextMessage> TextMessages { get; } = [];

        public List<ChannelAudioMessage> AudioMessages { get; } = [];

        public Task MarkMessageReadAsync(ChannelMessageReference message, CancellationToken cancellationToken)
        {
            ReadMessages.Add(message);
            return Task.CompletedTask;
        }

        public Task<DownloadedMedia> DownloadMediaAsync(ChannelMediaReference media, CancellationToken cancellationToken)
        {
            return Task.FromResult(new DownloadedMedia(
                new MemoryStream(Encoding.UTF8.GetBytes("audio")),
                "audio/ogg",
                ".ogg",
                5));
        }

        public Task<SentMessageReference> SendTextAsync(ChannelTextMessage message, CancellationToken cancellationToken)
        {
            TextMessages.Add(message);
            return Task.FromResult(new SentMessageReference($"sent-text-{TextMessages.Count}"));
        }

        public Task<SentMessageReference> SendAudioAsync(ChannelAudioMessage message, CancellationToken cancellationToken)
        {
            AudioMessages.Add(message);
            return Task.FromResult(new SentMessageReference($"sent-audio-{AudioMessages.Count}"));
        }
    }

    private sealed class FakeTranscriptionIntegration : ITranscriptionIntegration
    {
        public int LastRequestBytes { get; private set; }

        public Task<TranscriptionResult> TranscribeAsync(TranscriptionRequest request, CancellationToken cancellationToken)
        {
            using var copy = new MemoryStream();
            request.Audio.CopyTo(copy);
            LastRequestBytes = (int)copy.Length;
            return Task.FromResult(new TranscriptionResult("Quiero reservar a las cuatro.", "es", TimeSpan.FromSeconds(2)));
        }
    }

    private sealed class FakeSpeechSynthesisIntegration : ISpeechSynthesisIntegration
    {
        public Task<SpeechSynthesisResult> SynthesizeAsync(SpeechSynthesisRequest request, CancellationToken cancellationToken)
        {
            return Task.FromResult(new SpeechSynthesisResult(
                new MemoryStream(Encoding.UTF8.GetBytes("tts")),
                "audio/mpeg",
                ".mp3"));
        }
    }

    private sealed class FakeAudioBlobStore : IAudioBlobStore
    {
        public List<AudioBlobStoreRequest> Stored { get; } = [];

        public bool ConsumeStream { get; set; }

        public Task<AudioBlobStoreResult> StoreAsync(AudioBlobStoreRequest request, CancellationToken cancellationToken)
        {
            if (ConsumeStream)
            {
                using var copy = new MemoryStream();
                request.Content.CopyTo(copy);
            }

            Stored.Add(request);
            return Task.FromResult(new AudioBlobStoreResult(new Uri($"https://blob.test/{request.Path}"), request.SizeBytes));
        }
    }

    private sealed class FakeAgentRuntime : IAgentRuntime
    {
        public List<AgentRunRequest> Requests { get; } = [];

        public Task<AgentRunResult> RunAsync(AgentRunRequest request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return Task.FromResult(new AgentRunResult("Claro, reviso disponibilidad.", []));
        }
    }
}
