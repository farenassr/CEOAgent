using System.Text.Json;
using CeoAgent.Application.Abstractions.AI;
using CeoAgent.Shared.AI;
using CeoAgent.Application.Abstractions.Company;
using CeoAgent.Infrastructure.Implementation.Company;
using CeoAgent.Infrastructure;
using CeoAgent.Infrastructure.Entities;
using CeoAgent.Infrastructure.Entities.JsonDocuments;
using CeoAgent.Application.Abstractions.AITools.GoogleCalendar;
using CeoAgent.Shared.Calendar;
using CeoAgent.Application.Abstractions.Jobs;
using CeoAgent.Shared.Jobs;
using CeoAgent.Application.Abstractions.Messaging;
using CeoAgent.Shared.Messaging;
using CeoAgent.Shared.Constants;
using CeoAgent.Shared.Enums;
using CeoAgent.Infrastructure.Implementation.AITools.Execution;
using CeoAgent.Shared.AITools;
using CeoAgent.Infrastructure.Implementation.AITools.GoogleCalendar;
using CeoAgent.Worker.Jobs;
using CeoAgent.Worker.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;

namespace CeoAgent.Worker.Tests.Jobs;

public sealed class ProcessIncomingMessageJobProcessorTests
{
    [Test]
    public async Task ProcessAsync_ForInboundAudio_SendsTextOnlyReplyWithoutCallingAgent()
    {
        await using var fixture = await ProcessorFixture.CreateAsync();

        await fixture.Processor.ProcessAsync(
            new ProcessIncomingMessageJob(
                fixture.CompanyId,
                fixture.Conversation.Id,
                fixture.InboundMessage.Id,
                "correlation-123"),
            CancellationToken.None);

        fixture.Messaging.ReadMessages.Single().ProviderMessageId.ShouldBe("wamid.audio-1");
        fixture.Messaging.TextMessages.Single().Text.ShouldBe("Por ahora solo puedo procesar mensajes de texto.");

        fixture.Agent.Requests.ShouldBeEmpty();

        var assistant = fixture.DbContext.ChangeTracker
            .Entries<Message>()
            .Select(entry => entry.Entity)
            .Single(message => message.Role == MessageRole.Assistant);
        assistant.Type.ShouldBe(MessageType.Text);
        assistant.MessageText.ShouldBe("Por ahora solo puedo procesar mensajes de texto.");
        assistant.Payload!.ProviderType.ShouldBe("text");
        assistant.Payload.ProviderMessageId.ShouldBe("sent-text-1");
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
        fixture.Agent.Requests.Count.ShouldBe(0);

        fixture.CompanyContext.SetCompany(fixture.CompanyId);
        var assistantCount = await fixture.DbContext.Messages
            .CountAsync(message => message.Role == MessageRole.Assistant);
        assistantCount.ShouldBe(1);
    }

    [Test]
    public async Task ProcessAsync_ForTextMessageWithoutProviderMessageId_SkipsReadReceiptAndSendsWhatsAppReply()
    {
        await using var fixture = await ProcessorFixture.CreateAsync();
        fixture.InboundMessage.Type = MessageType.Text;
        fixture.InboundMessage.MessageText = "Hola desde WhatsApp admin";
        fixture.InboundMessage.ProviderMessageId = null;
        fixture.InboundMessage.Payload = new MessagePayload
        {
            ProviderType = "whatsapp_cloud",
        };
        await fixture.DbContext.SaveChangesAsync();

        await fixture.Processor.ProcessAsync(
            new ProcessIncomingMessageJob(
                fixture.CompanyId,
                fixture.Conversation.Id,
                fixture.InboundMessage.Id,
                "correlation-123"),
            CancellationToken.None);

        fixture.Messaging.ReadMessages.ShouldBeEmpty();
        var request = fixture.Agent.Requests.Single();
        request.Messages[^1].Text.ShouldBe("Hola desde WhatsApp admin");
        fixture.Messaging.TextMessages.Single().Text.ShouldBe("Claro, reviso disponibilidad.");
        fixture.CompanyContext.SetCompany(fixture.CompanyId);
        var assistant = await fixture.DbContext.Messages
            .SingleAsync(message => message.Role == MessageRole.Assistant);
        assistant.MessageText.ShouldBe("Claro, reviso disponibilidad.");
        assistant.ProviderMessageId.ShouldBe($"reply:{fixture.InboundMessage.Id}");
        assistant.Payload!.ProviderMessageId.ShouldBe("sent-text-1");
    }

    [Test]
    public async Task ProcessAsync_ForTextMessageWithoutProviderMessageId_PersistsReplyWithSingleSaveChanges()
    {
        await using var fixture = await ProcessorFixture.CreateAsync();
        fixture.InboundMessage.Type = MessageType.Text;
        fixture.InboundMessage.MessageText = "Hola desde WhatsApp admin";
        fixture.InboundMessage.ProviderMessageId = null;
        fixture.InboundMessage.Payload = new MessagePayload
        {
            ProviderType = "whatsapp_cloud",
        };
        await fixture.DbContext.SaveChangesAsync();

        var saveChangesCount = 0;
        fixture.DbContext.SavingChanges += (_, _) => saveChangesCount++;

        await fixture.Processor.ProcessAsync(
            new ProcessIncomingMessageJob(
                fixture.CompanyId,
                fixture.Conversation.Id,
                fixture.InboundMessage.Id,
                "correlation-123"),
            CancellationToken.None);

        fixture.Messaging.ReadMessages.ShouldBeEmpty();
        fixture.Messaging.TextMessages.Single().Text.ShouldBe("Claro, reviso disponibilidad.");
        fixture.Agent.Requests.Count.ShouldBe(1);
        saveChangesCount.ShouldBe(1);

        var assistant = fixture.DbContext.ChangeTracker
            .Entries<Message>()
            .Select(entry => entry.Entity)
            .Single(message => message.Role == MessageRole.Assistant);
        assistant.ProviderMessageId.ShouldBe($"reply:{fixture.InboundMessage.Id}");
        assistant.Payload!.ProviderMessageId.ShouldBe("sent-text-1");
    }

    [Test]
    public async Task ProcessAsync_DoesNotTrackReadOnlyContextEntities()
    {
        await using var fixture = await ProcessorFixture.CreateAsync();
        fixture.InboundMessage.Type = MessageType.Text;
        fixture.InboundMessage.MessageText = "Hola desde WhatsApp admin";
        fixture.InboundMessage.ProviderMessageId = null;
        fixture.InboundMessage.Payload = new MessagePayload
        {
            ProviderType = "whatsapp_cloud",
        };
        await fixture.DbContext.SaveChangesAsync();
        fixture.DbContext.ChangeTracker.Clear();

        await fixture.Processor.ProcessAsync(
            new ProcessIncomingMessageJob(
                fixture.CompanyId,
                fixture.Conversation.Id,
                fixture.InboundMessage.Id,
                "correlation-123"),
            CancellationToken.None);

        fixture.DbContext.ChangeTracker.Entries<Company>().ShouldBeEmpty();
        fixture.DbContext.ChangeTracker.Entries<AgentProfile>().ShouldBeEmpty();
        fixture.DbContext.ChangeTracker.Entries<CompanyChannel>().ShouldBeEmpty();
        fixture.DbContext.ChangeTracker.Entries<Customer>().ShouldBeEmpty();
        fixture.DbContext.ChangeTracker.Entries<Conversation>().ShouldNotBeEmpty();
        fixture.DbContext.ChangeTracker.Entries<Message>().ShouldNotBeEmpty();
    }

    [Test]
    public async Task ProcessAsync_ForTextMessageWithoutProviderMessageId_ExecutesMutatingTool()
    {
        await using var fixture = await ProcessorFixture.CreateAsync();
        fixture.InboundMessage.Type = MessageType.Text;
        fixture.InboundMessage.MessageText = "Reserva para dos a las cuatro";
        fixture.InboundMessage.ProviderMessageId = null;
        fixture.InboundMessage.Payload = new MessagePayload
        {
            ProviderType = "whatsapp_cloud",
        };
        await fixture.DbContext.SaveChangesAsync();

        fixture.Agent.Results.Enqueue(new AgentRunResult(
            AssistantText: null,
            ToolCalls:
            [
                new AgentToolCall(
                    "call-reservation",
                    MvpToolKeys.CreateGoogleCalendarReservation,
                    System.Text.Json.JsonSerializer.SerializeToElement(new
                    {
                        start = "2026-05-28T16:00:00-05:00",
                        end = "2026-05-28T17:00:00-05:00",
                        summary = "Reservation for 2",
                        customerName = "Ada Lovelace",
                    })),
            ]));
        fixture.Agent.Results.Enqueue(new AgentRunResult("Reserva creada.", []));

        await fixture.Processor.ProcessAsync(
            new ProcessIncomingMessageJob(
                fixture.CompanyId,
                fixture.Conversation.Id,
                fixture.InboundMessage.Id,
                "correlation-123"),
            CancellationToken.None);

        fixture.Calendar.ReservationRequests.Count.ShouldBe(1);
        fixture.Messaging.TextMessages.Single().Text.ShouldBe("Reserva creada.");
        fixture.Agent.Requests.Count.ShouldBe(2);
        fixture.Agent.Requests[1].Messages.Any(message =>
            message.Role == "tool"
            && message.Text != null
            && message.Text.Contains("\"status\":\"succeeded\"", StringComparison.Ordinal)
            && message.Text.Contains("\"eventId\":\"event-123\"", StringComparison.Ordinal)).ShouldBeTrue();
    }

    [Test]
    public async Task ProcessAsync_ForTextMessageWithMutatingTool_PersistsAllDatabaseChangesWithSingleSaveChanges()
    {
        await using var fixture = await ProcessorFixture.CreateAsync();
        fixture.InboundMessage.Type = MessageType.Text;
        fixture.InboundMessage.MessageText = "Reserva para dos a las cuatro";
        fixture.InboundMessage.ProviderMessageId = null;
        fixture.InboundMessage.Payload = new MessagePayload
        {
            ProviderType = "whatsapp_cloud",
        };
        await fixture.DbContext.SaveChangesAsync();

        fixture.Agent.Results.Enqueue(new AgentRunResult(
            AssistantText: null,
            ToolCalls:
            [
                new AgentToolCall(
                    "call-reservation",
                    MvpToolKeys.CreateGoogleCalendarReservation,
                    JsonSerializer.SerializeToElement(new
                    {
                        start = "2026-05-28T16:00:00-05:00",
                        end = "2026-05-28T17:00:00-05:00",
                        summary = "Reservation for 2",
                        customerName = "Ada Lovelace",
                    })),
            ]));
        fixture.Agent.Results.Enqueue(new AgentRunResult("Reserva creada.", []));

        var saveChangesCount = 0;
        fixture.DbContext.SavingChanges += (_, _) => saveChangesCount++;

        await fixture.Processor.ProcessAsync(
            new ProcessIncomingMessageJob(
                fixture.CompanyId,
                fixture.Conversation.Id,
                fixture.InboundMessage.Id,
                "correlation-123"),
            CancellationToken.None);

        saveChangesCount.ShouldBe(1);
        fixture.Calendar.ReservationRequests.Count.ShouldBe(1);
        fixture.CompanyContext.SetCompany(fixture.CompanyId);
        (await fixture.DbContext.ToolExecutions.CountAsync()).ShouldBe(1);
        (await fixture.DbContext.Messages.CountAsync(message => message.Role == MessageRole.ToolResult)).ShouldBe(1);
        (await fixture.DbContext.Messages.CountAsync(message => message.Role == MessageRole.Assistant)).ShouldBe(1);
    }

    [Test]
    public async Task ProcessAsync_ExcludesPersistedToolMessagesFromPromptHistory()
    {
        await using var fixture = await ProcessorFixture.CreateAsync();
        fixture.InboundMessage.Type = MessageType.Text;
        fixture.InboundMessage.MessageText = "Nuevo turno";
        fixture.InboundMessage.Payload = null;

        fixture.DbContext.Messages.AddRange(
            new Message
            {
                CompanyId = fixture.CompanyId,
                ConversationId = fixture.Conversation.Id,
                Role = MessageRole.ToolCall,
                Type = MessageType.Text,
                MessageText = MvpToolKeys.CheckGoogleCalendarAvailability,
                OccurredAt = new DateTime(2026, 5, 28, 21, 1, 0, DateTimeKind.Utc),
            },
            new Message
            {
                CompanyId = fixture.CompanyId,
                ConversationId = fixture.Conversation.Id,
                Role = MessageRole.ToolResult,
                Type = MessageType.Text,
                MessageText = """{"toolKey":"check_google_calendar_availability","status":"succeeded"}""",
                OccurredAt = new DateTime(2026, 5, 28, 21, 2, 0, DateTimeKind.Utc),
            });
        await fixture.DbContext.SaveChangesAsync();

        await fixture.Processor.ProcessAsync(
            new ProcessIncomingMessageJob(
                fixture.CompanyId,
                fixture.Conversation.Id,
                fixture.InboundMessage.Id,
                "correlation-123"),
            CancellationToken.None);

        var request = fixture.Agent.Requests.Single();
        request.Messages.Any(message =>
            string.Equals(message.Text, MvpToolKeys.CheckGoogleCalendarAvailability, StringComparison.Ordinal)
            || (message.Text != null && message.Text.Contains("\"toolKey\"", StringComparison.Ordinal))).ShouldBeFalse();
    }

    [Test]
    public async Task ProcessAsync_WhenAgentRuntimeFails_SendsFallbackReply()
    {
        await using var fixture = await ProcessorFixture.CreateAsync();
        fixture.InboundMessage.Type = MessageType.Text;
        fixture.InboundMessage.MessageText = "Hola";
        fixture.InboundMessage.Payload = null;
        await fixture.DbContext.SaveChangesAsync();
        fixture.Agent.ThrowOnRun = new InvalidOperationException("runtime unavailable");

        await fixture.Processor.ProcessAsync(
            new ProcessIncomingMessageJob(
                fixture.CompanyId,
                fixture.Conversation.Id,
                fixture.InboundMessage.Id,
                "correlation-123"),
            CancellationToken.None);

        fixture.Messaging.TextMessages.Single().Text.ShouldBe("No pude completar la accion automatica. Te pondre en contacto con una persona del equipo.");
    }

    [Test]
    public async Task ProcessAsync_WhenModelRequestsCalendarTool_ExecutesToolThenSendsFinalReply()
    {
        await using var fixture = await ProcessorFixture.CreateAsync();
        fixture.InboundMessage.Type = MessageType.Text;
        fixture.InboundMessage.MessageText = "Quiero reservar a las cuatro.";
        fixture.InboundMessage.Payload = null;
        await fixture.DbContext.SaveChangesAsync();

        fixture.Agent.Results.Enqueue(new AgentRunResult(
            AssistantText: null,
            ToolCalls:
            [
                new AgentToolCall(
                    "call-availability",
                    MvpToolKeys.CheckGoogleCalendarAvailability,
                    System.Text.Json.JsonSerializer.SerializeToElement(new
                    {
                        date = "2026-05-28",
                        partySize = 2,
                        preferredTime = "16:00",
                    })),
            ]));
        fixture.Agent.Results.Enqueue(new AgentRunResult("Si, tenemos disponibilidad a las 4:00 p.m.", []));

        await fixture.Processor.ProcessAsync(
            new ProcessIncomingMessageJob(
                fixture.CompanyId,
                fixture.Conversation.Id,
                fixture.InboundMessage.Id,
                "correlation-123"),
            CancellationToken.None);

        fixture.Agent.Requests.Count.ShouldBe(2);
        fixture.Agent.Requests[1].Messages.Any(message =>
            message.Role == "tool"
            && message.ToolCallId == "call-availability"
            && message.Text is not null
            && message.Text.Contains("\"status\":\"succeeded\"", StringComparison.Ordinal)).ShouldBeTrue();
        fixture.Messaging.TextMessages.Single().Text.ShouldBe("Si, tenemos disponibilidad a las 4:00 p.m.");
        fixture.Calendar.AvailabilityRequests.Count.ShouldBe(1);
    }

    [Test]
    public async Task ProcessAsync_WhenModelFindsReservations_IncludesReservationTimeInToolResult()
    {
        await using var fixture = await ProcessorFixture.CreateAsync();
        fixture.InboundMessage.Type = MessageType.Text;
        fixture.InboundMessage.MessageText = "Tengo una reserva hoy?";
        fixture.InboundMessage.Payload = null;
        await fixture.DbContext.SaveChangesAsync();
        fixture.Calendar.FindResult = new CalendarReservationSearchResult(
        [
            new CalendarReservationInfo(
                "reservation-key",
                "event-123",
                new DateTimeOffset(2026, 5, 28, 16, 0, 0, TimeSpan.FromHours(-5)),
                new DateTimeOffset(2026, 5, 28, 17, 0, 0, TimeSpan.FromHours(-5)),
                "Reservation for 2",
                "Ada Lovelace",
                "https://calendar.google.com/event?eid=event-123"),
        ]);

        fixture.Agent.Results.Enqueue(new AgentRunResult(
            AssistantText: null,
            ToolCalls:
            [
                new AgentToolCall(
                    "call-find",
                    MvpToolKeys.FindGoogleCalendarReservations,
                    JsonSerializer.SerializeToElement(new
                    {
                        date = "2026-05-28",
                        includePast = false,
                        status = (string?)null,
                    })),
            ]));
        fixture.Agent.Results.Enqueue(new AgentRunResult("Tu reserva es a las 4:00 p.m.", []));

        await fixture.Processor.ProcessAsync(
            new ProcessIncomingMessageJob(
                fixture.CompanyId,
                fixture.Conversation.Id,
                fixture.InboundMessage.Id,
                "correlation-123"),
            CancellationToken.None);

        fixture.Agent.Requests.Count.ShouldBe(2);
        fixture.Agent.Requests[1].Messages.Any(message =>
            message.Role == "tool"
            && message.ToolCallId == "call-find"
            && message.Text is not null
            && message.Text.Contains("\"count\":1", StringComparison.Ordinal)
            && message.Text.Contains("2026-05-28T16:00:00-05:00", StringComparison.Ordinal)).ShouldBeTrue();
        fixture.Messaging.TextMessages.Single().Text.ShouldBe("Tu reserva es a las 4:00 p.m.");
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
            Agent = new FakeAgentRuntime();
            Calendar = new FakeCalendarIntegration();
            var calendarExecutor = new GoogleCalendarToolExecutor(
                DbContext,
                Calendar,
                new FixedTimeProvider(new DateTimeOffset(2026, 5, 27, 12, 0, 0, TimeSpan.Zero)));
            var toolRegistry = new CompanyToolRegistry(DbContext);
            var helper = new ToolExecutionGatewayHelper(DbContext);
            var executors = new IToolExecutor[]
            {
                new CheckGoogleCalendarAvailabilityExecutor(calendarExecutor, helper),
                new CreateGoogleCalendarReservationExecutor(calendarExecutor, helper),
                new FindGoogleCalendarReservationsExecutor(calendarExecutor, helper),
                new UpdateGoogleCalendarReservationExecutor(calendarExecutor, helper),
                new CancelGoogleCalendarReservationExecutor(calendarExecutor, helper)
            };
            var toolGateway = new ToolExecutionGateway(executors, helper);
            Processor = new ProcessIncomingMessageJobProcessor(
                DbContext,
                Messaging,
                Agent,
                toolRegistry,
                toolGateway,
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
                Payload = new MessagePayload
                {
                    ProviderType = "audio",
                    ProviderMessageId = "wamid.audio-1",
                },
                OccurredAt = new DateTime(2026, 5, 28, 21, 0, 0, DateTimeKind.Utc),
            };

            var credential = new IntegrationCredentialReference
            {
                Id = Guid.Parse("018f4f70-8b5f-7b4c-9d1a-0f6c1d7a2b42"),
                CompanyId = CompanyId,
                Provider = IntegrationProvider.GoogleCalendar,
                Purpose = "google_calendar",
                Reference = "config://GoogleCalendar:ServiceAccountJson",
            };

            var checkTool = new CompanyTool
            {
                Id = Guid.Parse("018f4f70-8b5f-7b4c-9d1a-0f6c1d7a2b40"),
                CompanyId = CompanyId,
                ToolKey = MvpToolKeys.CheckGoogleCalendarAvailability,
                Description = "Check Google Calendar availability before offering or confirming reservation times.",
                ParametersSchema = ParseSchema("""{"type":"object","properties":{"date":{"type":"string"},"partySize":{"type":"integer"},"preferredTime":{"type":["string","null"]}},"required":["date","partySize","preferredTime"],"additionalProperties":false}"""),
                IsEnabled = true,
                CredentialReferenceId = credential.Id,
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
                Description = "Create a Google Calendar reservation after explicit customer confirmation.",
                ParametersSchema = ParseSchema("""{"type":"object","properties":{"start":{"type":"string"},"end":{"type":"string"},"summary":{"type":"string"},"customerName":{"type":"string"}},"required":["start","end","summary","customerName"],"additionalProperties":false}"""),
                IsEnabled = true,
                CredentialReferenceId = credential.Id,
                Configuration = ToolConfiguration.ForGoogleCalendar(new GoogleCalendarConfig
                {
                    CalendarId = "primary",
                    TimeZoneId = "America/Bogota",
                    BufferMinutes = 0,
                }),
            };
            var findTool = new CompanyTool
            {
                Id = Guid.Parse("018f4f70-8b5f-7b4c-9d1a-0f6c1d7a2b43"),
                CompanyId = CompanyId,
                ToolKey = MvpToolKeys.FindGoogleCalendarReservations,
                Description = "Find reservations for the current WhatsApp customer.",
                ParametersSchema = ParseSchema("""{"type":"object","properties":{"date":{"type":["string","null"]},"includePast":{"type":"boolean"},"status":{"type":["string","null"]}},"required":["date","includePast","status"],"additionalProperties":false}"""),
                IsEnabled = true,
                CredentialReferenceId = credential.Id,
                Configuration = ToolConfiguration.ForGoogleCalendar(new GoogleCalendarConfig
                {
                    CalendarId = "primary",
                    TimeZoneId = "America/Bogota",
                    BufferMinutes = 0,
                }),
            };
            DbContext.AddRange(company, profile, Channel, Customer, Conversation, InboundMessage, credential, checkTool, createTool, findTool);
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

        public FakeAgentRuntime Agent { get; }

        public FakeCalendarIntegration Calendar { get; }

        public ProcessIncomingMessageJobProcessor Processor { get; }

        public CompanyChannel Channel { get; }

        public Customer Customer { get; }

        public Conversation Conversation { get; }

        public Message InboundMessage { get; }

        public async ValueTask DisposeAsync()
        {
            await database.DisposeAsync();
        }

        private static JsonElement ParseSchema(string json)
        {
            using var document = JsonDocument.Parse(json);
            return document.RootElement.Clone();
        }
    }

    private sealed class FakeMessageChannelIntegration : IMessageChannelIntegration
    {
        public List<ChannelMessageReference> ReadMessages { get; } = [];

        public List<ChannelTextMessage> TextMessages { get; } = [];

        public Task MarkMessageReadAsync(ChannelMessageReference message, CancellationToken cancellationToken)
        {
            ReadMessages.Add(message);
            return Task.CompletedTask;
        }

        public Task<SentMessageReference> SendTextAsync(ChannelTextMessage message, CancellationToken cancellationToken)
        {
            TextMessages.Add(message);
            return Task.FromResult(new SentMessageReference($"sent-text-{TextMessages.Count}"));
        }
    }

    private sealed class FakeAgentRuntime : IAgentRuntime
    {
        public List<AgentRunRequest> Requests { get; } = [];

        public Queue<AgentRunResult> Results { get; } = [];

        public Exception? ThrowOnRun { get; set; }

        public Task<AgentRunResult> RunAsync(AgentRunRequest request, CancellationToken cancellationToken)
        {
            if (ThrowOnRun is not null)
            {
                throw ThrowOnRun;
            }

            Requests.Add(request);
            return Task.FromResult(Results.Count > 0
                ? Results.Dequeue()
                : new AgentRunResult("Claro, reviso disponibilidad.", []));
        }
    }

    private sealed class FakeCalendarIntegration : IGoogleCalendarIntegration
    {
        public List<CalendarAvailabilityRequest> AvailabilityRequests { get; } = [];

        public List<CalendarReservationRequest> ReservationRequests { get; } = [];

        public List<CalendarReservationSearchRequest> FindRequests { get; } = [];

        public CalendarReservationSearchResult FindResult { get; set; } = new([]);

        public Task<CalendarAvailabilityResult> CheckAvailabilityAsync(
            CalendarAvailabilityRequest request,
            CancellationToken cancellationToken)
        {
            AvailabilityRequests.Add(request);
            return Task.FromResult(new CalendarAvailabilityResult(true, [], null));
        }

        public Task<CalendarReservationResult> CreateReservationAsync(
            CalendarReservationRequest request,
            CancellationToken cancellationToken)
        {
            ReservationRequests.Add(request);
            return Task.FromResult(new CalendarReservationResult("event-123", "https://calendar.google.com/event?eid=event-123"));
        }

        public Task<CalendarReservationSearchResult> FindReservationsAsync(
            CalendarReservationSearchRequest request,
            CancellationToken cancellationToken)
        {
            FindRequests.Add(request);
            return Task.FromResult(FindResult);
        }

        public Task<CalendarReservationMutationResult> UpdateReservationAsync(
            CalendarReservationUpdateRequest request,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(CalendarReservationMutationResult.NotOwned(request.ReservationId));
        }

        public Task<CalendarReservationCancellationResult> CancelReservationAsync(
            CalendarReservationCancellationRequest request,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(CalendarReservationCancellationResult.NotOwned(request.ReservationId));
        }
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow()
        {
            return utcNow;
        }
    }
}
