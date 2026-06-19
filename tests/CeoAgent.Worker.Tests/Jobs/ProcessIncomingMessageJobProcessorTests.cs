using System.Diagnostics;
using System.Text.Json;
using CeoAgent.Application;
using CeoAgent.Application.Abstractions.AI;
using CeoAgent.Shared.AI;
using CeoAgent.Application.Abstractions.Payments;
using CeoAgent.Application.Abstractions.Organization;
using CeoAgent.Shared.Storage;
using CeoAgent.Infrastructure.Implementation.Organization;
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
using CeoAgent.Shared.Payment;
using CeoAgent.Infrastructure.Implementation.AITools.Execution;
using CeoAgent.Infrastructure.Implementation.AITools.GoogleCalendar;
using CeoAgent.Infrastructure.Implementation.AITools.Handoff;
using CeoAgent.Infrastructure.Implementation.Messaging;
using CeoAgent.Worker.Jobs;
using CeoAgent.Worker.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
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
                fixture.OrganizationId,
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
            fixture.OrganizationId,
            fixture.Conversation.Id,
            fixture.InboundMessage.Id,
            "correlation-123");

        await fixture.Processor.ProcessAsync(job, CancellationToken.None);
        await fixture.Processor.ProcessAsync(job, CancellationToken.None);

        fixture.Messaging.ReadMessages.Count.ShouldBe(1);
        fixture.Messaging.TextMessages.Count.ShouldBe(1);
        fixture.Agent.Requests.Count.ShouldBe(0);

        fixture.OrganizationContext.SetOrganization(fixture.OrganizationId);
        var assistantCount = await fixture.DbContext.Messages
            .CountAsync(message => message.Role == MessageRole.Assistant);
        assistantCount.ShouldBe(1);
        var dispatch = await fixture.DbContext.MessageDispatches.SingleAsync();
        dispatch.Operation.ShouldBe(MessageDispatchOperation.OutboundProviderSend);
        dispatch.Status.ShouldBe(MessageDispatchStatus.Succeeded);
        dispatch.AttemptCount.ShouldBe(1);
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
                fixture.OrganizationId,
                fixture.Conversation.Id,
                fixture.InboundMessage.Id,
                "correlation-123"),
            CancellationToken.None);

        fixture.Messaging.ReadMessages.ShouldBeEmpty();
        var request = fixture.Agent.Requests.Single();
        request.MaxOutputTokenCount.ShouldBe(1024);
        request.UserMessage.ShouldBe("Hola desde WhatsApp admin");
        request.OrganizationId.ShouldBe(fixture.OrganizationId);
        request.ConversationId.ShouldBe(fixture.Conversation.Id);
        request.InboundMessageId.ShouldBe(fixture.InboundMessage.Id);
        request.CorrelationId.ShouldBe("correlation-123");
        fixture.Messaging.TextMessages.Single().Text.ShouldBe("Claro, reviso disponibilidad.");
        fixture.OrganizationContext.SetOrganization(fixture.OrganizationId);
        var assistant = await fixture.DbContext.Messages
            .SingleAsync(message => message.Role == MessageRole.Assistant);
        assistant.MessageText.ShouldBe("Claro, reviso disponibilidad.");
        assistant.ProviderMessageId.ShouldBe($"reply:{fixture.InboundMessage.Id}");
        assistant.Payload!.ProviderMessageId.ShouldBe("sent-text-1");
    }

    [Test]
    public async Task ProcessAsync_ForTextMessageWithoutProviderMessageId_PersistsReplyBeforeSendAndProviderReferenceAfterSend()
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
                fixture.OrganizationId,
                fixture.Conversation.Id,
                fixture.InboundMessage.Id,
                "correlation-123"),
            CancellationToken.None);

        fixture.Messaging.ReadMessages.ShouldBeEmpty();
        fixture.Messaging.TextMessages.Single().Text.ShouldBe("Claro, reviso disponibilidad.");
        fixture.Agent.Requests.Count.ShouldBe(1);
        saveChangesCount.ShouldBe(2);

        var assistant = fixture.DbContext.ChangeTracker
            .Entries<Message>()
            .Select(entry => entry.Entity)
            .Single(message => message.Role == MessageRole.Assistant);
        assistant.ProviderMessageId.ShouldBe($"reply:{fixture.InboundMessage.Id}");
        assistant.Payload!.ProviderMessageId.ShouldBe("sent-text-1");

        fixture.OrganizationContext.SetOrganization(fixture.OrganizationId);
        var dispatch = await fixture.DbContext.MessageDispatches.SingleAsync();
        dispatch.OrganizationId.ShouldBe(fixture.OrganizationId);
        dispatch.ConversationId.ShouldBe(fixture.Conversation.Id);
        dispatch.MessageId.ShouldBe(assistant.Id);
        dispatch.Operation.ShouldBe(MessageDispatchOperation.OutboundProviderSend);
        dispatch.Provider.ShouldBe("whatsapp_cloud");
        dispatch.Status.ShouldBe(MessageDispatchStatus.Succeeded);
        dispatch.IdempotencyKey.ShouldBe($"reply:{fixture.InboundMessage.Id}");
        dispatch.ProviderMessageId.ShouldBe("sent-text-1");
        dispatch.CorrelationId.ShouldBe("correlation-123");
        dispatch.AttemptCount.ShouldBe(1);
    }

    [Test]
    public async Task ProcessAsync_WhenProviderSendFails_LeavesOutboundDispatchRetryScheduled()
    {
        await using var fixture = await ProcessorFixture.CreateAsync();
        fixture.InboundMessage.Type = MessageType.Text;
        fixture.InboundMessage.MessageText = "Hola desde WhatsApp admin";
        fixture.InboundMessage.ProviderMessageId = null;
        fixture.InboundMessage.Payload = new MessagePayload
        {
            ProviderType = "whatsapp_cloud",
        };
        fixture.Messaging.ThrowOnSendText = new InvalidOperationException("provider unavailable");
        await fixture.DbContext.SaveChangesAsync();

        await Should.ThrowAsync<InvalidOperationException>(() => fixture.Processor.ProcessAsync(
            new ProcessIncomingMessageJob(
                fixture.OrganizationId,
                fixture.Conversation.Id,
                fixture.InboundMessage.Id,
                "correlation-123"),
            CancellationToken.None));

        fixture.OrganizationContext.SetOrganization(fixture.OrganizationId);
        var assistant = await fixture.DbContext.Messages.SingleAsync(message => message.Role == MessageRole.Assistant);
        assistant.Payload!.ProviderMessageId.ShouldBeNull();
        var dispatch = await fixture.DbContext.MessageDispatches.SingleAsync();
        dispatch.Operation.ShouldBe(MessageDispatchOperation.OutboundProviderSend);
        dispatch.Status.ShouldBe(MessageDispatchStatus.RetryScheduled);
        dispatch.AttemptCount.ShouldBe(1);
        dispatch.LastError.ShouldBe("provider unavailable");
        dispatch.ProviderMessageId.ShouldBeNull();
        dispatch.CorrelationId.ShouldBe("correlation-123");
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
                fixture.OrganizationId,
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
    public async Task ProcessAsync_WhenAwaitingPaymentAndInboundImageArrives_HandsOffWithoutCallingAgent()
    {
        await using var fixture = await ProcessorFixture.CreateAsync();
        await fixture.AddAwaitingPaymentStateAsync();
        fixture.InboundMessage.Type = MessageType.Image;
        fixture.InboundMessage.MessageText = null;
        fixture.InboundMessage.ProviderMessageId = "wamid.image-1";
        fixture.InboundMessage.Payload = new MessagePayload
        {
            ProviderType = "image",
            ProviderMessageId = "wamid.image-1",
            ProviderMediaId = "media-123",
        };
        await fixture.DbContext.SaveChangesAsync();

        await fixture.Processor.ProcessAsync(
            new ProcessIncomingMessageJob(
                fixture.OrganizationId,
                fixture.Conversation.Id,
                fixture.InboundMessage.Id,
                "correlation-123"),
            CancellationToken.None);

        fixture.Agent.Requests.ShouldBeEmpty();
        fixture.Messaging.TextMessages.ShouldContain(message =>
            message.RecipientExternalId == fixture.Customer.ExternalCustomerId
            && message.Text.Contains("confirmacion", StringComparison.OrdinalIgnoreCase));

        fixture.OrganizationContext.SetOrganization(fixture.OrganizationId);
        var conversation = await fixture.DbContext.Conversations.SingleAsync(entity => entity.Id == fixture.Conversation.Id);
        conversation.Status.ShouldBe(ConversationStatus.HandedOff);
        var state = await fixture.DbContext.ConversationStates.SingleAsync(entity => entity.ConversationId == fixture.Conversation.Id);
        state.Snapshot.PendingAction.ShouldBe("reservation_payment_receipt_received");
        state.Snapshot.ConversationFlags.ShouldContain("reservation_payment_receipt_received");
        var receiptMessage = await fixture.DbContext.Messages
            .SingleAsync(message => message.ProviderMessageId == $"payment-receipt:{fixture.InboundMessage.Id:N}");
        var receiptDispatch = await fixture.DbContext.MessageDispatches
            .SingleAsync(entity => entity.MessageId == receiptMessage.Id);
        receiptDispatch.Operation.ShouldBe(MessageDispatchOperation.OutboundProviderSend);
        receiptDispatch.Status.ShouldBe(MessageDispatchStatus.Succeeded);
    }

    [Test]
    public async Task ProcessAsync_WhenAwaitingPaymentAndCustomerSaysAlreadyPaid_HandsOffWithoutCallingAgent()
    {
        await using var fixture = await ProcessorFixture.CreateAsync();
        await fixture.AddAwaitingPaymentStateAsync();
        fixture.InboundMessage.Type = MessageType.Text;
        fixture.InboundMessage.MessageText = "Ya pague y te envie el comprobante";
        fixture.InboundMessage.ProviderMessageId = "wamid.text-1";
        fixture.InboundMessage.Payload = new MessagePayload
        {
            ProviderType = "text",
            ProviderMessageId = "wamid.text-1",
        };
        await fixture.DbContext.SaveChangesAsync();

        await fixture.Processor.ProcessAsync(
            new ProcessIncomingMessageJob(
                fixture.OrganizationId,
                fixture.Conversation.Id,
                fixture.InboundMessage.Id,
                "correlation-123"),
            CancellationToken.None);

        fixture.Agent.Requests.ShouldBeEmpty();
        fixture.OrganizationContext.SetOrganization(fixture.OrganizationId);
        var conversation = await fixture.DbContext.Conversations.SingleAsync(entity => entity.Id == fixture.Conversation.Id);
        conversation.Status.ShouldBe(ConversationStatus.HandedOff);
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
                OrganizationId = fixture.OrganizationId,
                ConversationId = fixture.Conversation.Id,
                Role = MessageRole.ToolCall,
                Type = MessageType.Text,
                MessageText = MvpToolKeys.CheckGoogleCalendarAvailability,
                OccurredAt = new DateTime(2026, 5, 28, 21, 1, 0, DateTimeKind.Utc),
            },
            new Message
            {
                OrganizationId = fixture.OrganizationId,
                ConversationId = fixture.Conversation.Id,
                Role = MessageRole.ToolResult,
                Type = MessageType.Text,
                MessageText = """{"toolKey":"check_google_calendar_availability","status":"succeeded"}""",
                OccurredAt = new DateTime(2026, 5, 28, 21, 2, 0, DateTimeKind.Utc),
            });
        await fixture.DbContext.SaveChangesAsync();

        await fixture.Processor.ProcessAsync(
            new ProcessIncomingMessageJob(
                fixture.OrganizationId,
                fixture.Conversation.Id,
                fixture.InboundMessage.Id,
                "correlation-123"),
            CancellationToken.None);

        var request = fixture.Agent.Requests.Single();
        request.UserMessage.ShouldBe("Nuevo turno");
    }

    [Test]
    public async Task ProcessAsync_WhenAgentRuntimeFails_SendsFallbackReplyAndHandsOff()
    {
        await using var fixture = await ProcessorFixture.CreateAsync();
        fixture.InboundMessage.Type = MessageType.Text;
        fixture.InboundMessage.MessageText = "Hola";
        fixture.InboundMessage.Payload = null;
        await fixture.DbContext.SaveChangesAsync();
        fixture.Agent.ThrowOnRun = new InvalidOperationException("runtime unavailable");

        await fixture.Processor.ProcessAsync(
            new ProcessIncomingMessageJob(
                fixture.OrganizationId,
                fixture.Conversation.Id,
                fixture.InboundMessage.Id,
                "correlation-123"),
            CancellationToken.None);

        fixture.Messaging.TextMessages.ShouldContain(message =>
            message.RecipientExternalId == fixture.Customer.ExternalCustomerId
            && message.Text == "No pude completar la accion automatica. Te pondre en contacto con una persona del equipo.");
        fixture.Messaging.TextMessages.ShouldContain(message =>
            message.RecipientExternalId == "15559998888"
            && message.Text.Contains("Atencion humana requerida.", StringComparison.Ordinal));
        fixture.OrganizationContext.SetOrganization(fixture.OrganizationId);
        await fixture.DbContext.Entry(fixture.Conversation).ReloadAsync();
        fixture.Conversation.Status.ShouldBe(ConversationStatus.HandedOff);
        (await fixture.DbContext.ToolExecutions.CountAsync(execution =>
            execution.ToolKey == MvpToolKeys.RequestHumanHandoff)).ShouldBe(1);
    }

    [Test]
    public async Task ProcessAsync_WhenConversationAlreadyHandedOff_SuppressesBot()
    {
        await using var fixture = await ProcessorFixture.CreateAsync();
        fixture.InboundMessage.Type = MessageType.Text;
        fixture.InboundMessage.MessageText = "Sigo aqui";
        fixture.InboundMessage.ProviderMessageId = "wamid.text-1";
        fixture.InboundMessage.Payload = new MessagePayload
        {
            ProviderType = "text",
            ProviderMessageId = "wamid.text-1",
        };
        fixture.Conversation.Status = ConversationStatus.HandedOff;
        await fixture.DbContext.SaveChangesAsync();

        await fixture.Processor.ProcessAsync(
            new ProcessIncomingMessageJob(
                fixture.OrganizationId,
                fixture.Conversation.Id,
                fixture.InboundMessage.Id,
                "correlation-123"),
            CancellationToken.None);

        fixture.Agent.Requests.ShouldBeEmpty();
        fixture.Messaging.TextMessages.ShouldBeEmpty();
        fixture.Messaging.ReadMessages.Count.ShouldBe(1);

        fixture.OrganizationContext.SetOrganization(fixture.OrganizationId);
        (await fixture.DbContext.Messages.CountAsync(message => message.Role == MessageRole.Assistant)).ShouldBe(0);
    }

    [Test]
    public async Task ProcessAsync_WhenProductionBudgetHasNoPricing_DoesNotCallAgentAndHandsOff()
    {
        await using var fixture = await ProcessorFixture.CreateAsync("Production");
        fixture.Agent.CanEstimateCosts = false;
        fixture.InboundMessage.Type = MessageType.Text;
        fixture.InboundMessage.MessageText = "Hola desde WhatsApp";
        fixture.InboundMessage.ProviderMessageId = null;
        fixture.InboundMessage.Payload = new MessagePayload
        {
            ProviderType = "whatsapp_cloud",
        };
        await fixture.DbContext.SaveChangesAsync();

        await fixture.Processor.ProcessAsync(
            new ProcessIncomingMessageJob(
                fixture.OrganizationId,
                fixture.Conversation.Id,
                fixture.InboundMessage.Id,
                "correlation-123"),
            CancellationToken.None);

        fixture.Agent.Requests.ShouldBeEmpty();
        fixture.Messaging.TextMessages.ShouldContain(message =>
            message.RecipientExternalId == fixture.Customer.ExternalCustomerId
            && message.Text == "No pude completar la accion automatica. Te pondre en contacto con una persona del equipo.");
        fixture.OrganizationContext.SetOrganization(fixture.OrganizationId);
        var conversation = await fixture.DbContext.Conversations.SingleAsync(entity => entity.Id == fixture.Conversation.Id);
        conversation.Status.ShouldBe(ConversationStatus.HandedOff);
    }

    [Test]
    public async Task ProcessAsync_WhenProductionBudgetIsActive_DisablesMutatingToolsForAgentTurn()
    {
        await using var fixture = await ProcessorFixture.CreateAsync("Production");
        fixture.InboundMessage.Type = MessageType.Text;
        fixture.InboundMessage.MessageText = "Cambia mi reserva a las 8";
        fixture.InboundMessage.ProviderMessageId = null;
        fixture.InboundMessage.Payload = new MessagePayload
        {
            ProviderType = "whatsapp_cloud",
        };
        await fixture.DbContext.SaveChangesAsync();

        await fixture.Processor.ProcessAsync(
            new ProcessIncomingMessageJob(
                fixture.OrganizationId,
                fixture.Conversation.Id,
                fixture.InboundMessage.Id,
                "correlation-123"),
            CancellationToken.None);

        var request = fixture.Agent.Requests.Single();
        request.MutatingToolsEnabled.ShouldBeFalse();
        request.MutatingToolsDisabledReason.ShouldBe("llm_budget_guard_active");
    }

    [Test]
    public async Task ProcessAsync_WhenEstimatedLlmCostExceedsProfileBudget_HandsOff()
    {
        await using var fixture = await ProcessorFixture.CreateAsync();
        fixture.InboundMessage.Type = MessageType.Text;
        fixture.InboundMessage.MessageText = "Hola desde WhatsApp";
        fixture.InboundMessage.ProviderMessageId = null;
        fixture.InboundMessage.Payload = new MessagePayload
        {
            ProviderType = "whatsapp_cloud",
        };
        fixture.Agent.Results.Enqueue(new AgentTurnResult(
            "Respuesta costosa",
            EstimatedCostUsd: 0.06d));
        await fixture.DbContext.SaveChangesAsync();

        await fixture.Processor.ProcessAsync(
            new ProcessIncomingMessageJob(
                fixture.OrganizationId,
                fixture.Conversation.Id,
                fixture.InboundMessage.Id,
                "correlation-123"),
            CancellationToken.None);

        fixture.Agent.Requests.Count.ShouldBe(1);
        fixture.Messaging.TextMessages.ShouldContain(message =>
            message.RecipientExternalId == fixture.Customer.ExternalCustomerId
            && message.Text == "No pude completar la accion automatica. Te pondre en contacto con una persona del equipo.");
        fixture.OrganizationContext.SetOrganization(fixture.OrganizationId);
        var conversation = await fixture.DbContext.Conversations.SingleAsync(entity => entity.Id == fixture.Conversation.Id);
        conversation.Status.ShouldBe(ConversationStatus.HandedOff);
    }

    private sealed class ProcessorFixture
    {
        private readonly PostgresWorkerDatabase database;

        private ProcessorFixture(PostgresWorkerDatabase database, string environmentName)
        {
            this.database = database;
            OrganizationContext = database.OrganizationContext;
            OrganizationContext.SetOrganization(OrganizationId);
            DbContext = database.Context;
            Messaging = new FakeMessageChannelIntegration();
            PaymentQrImages = new FakePaymentQrImageProvider();
            Agent = new FakeAgentRuntime();
            Calendar = new FakeCalendarIntegration();
            var handoffExecutor = new HumanHandoffToolExecutor(
                DbContext,
                Messaging,
                TimeProvider.System,
                NullLogger<HumanHandoffToolExecutor>.Instance);
            var outboundMessageDispatcher = new OutboundMessageDispatcher(
                DbContext,
                Messaging,
                TimeProvider.System,
                NullLogger<OutboundMessageDispatcher>.Instance);
            var paymentSender = new ReservationPaymentInstructionSender(
                DbContext,
                outboundMessageDispatcher,
                PaymentQrImages,
                handoffExecutor,
                TimeProvider.System,
                NullLogger<ReservationPaymentInstructionSender>.Instance);
            Processor = new ProcessIncomingMessageJobProcessor(
                DbContext,
                Messaging,
                outboundMessageDispatcher,
                Agent,
                handoffExecutor,
                paymentSender,
                OrganizationContext,
                TimeProvider.System,
                new FakeHostEnvironment(environmentName),
                NullLogger<ProcessIncomingMessageJobProcessor>.Instance);

            var company = new Company
            {
                Id = OrganizationId,
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
                OrganizationId = OrganizationId,
                ModelName = "gpt-4.1-mini",
                DisplayName = "Contoso Assistant",
                Language = "es",
                PromptOverride = "Responde corto.",
            };

            Channel = CompanyChannel.ForWhatsAppCloud(
                OrganizationId,
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
                OrganizationId = OrganizationId,
                CompanyChannelId = Channel.Id,
                ExternalCustomerId = "15551234567",
            };

            Conversation = new Conversation
            {
                Id = Guid.Parse("018f4f70-8b5f-7b4c-9d1a-0f6c1d7a2b34"),
                OrganizationId = OrganizationId,
                CustomerId = Customer.Id,
                CompanyChannelId = Channel.Id,
                AgentProfileId = profile.Id,
                LastMessageAt = new DateTime(2026, 5, 28, 21, 0, 0, DateTimeKind.Utc),
            };

            InboundMessage = new Message
            {
                Id = Guid.Parse("018f4f70-8b5f-7b4c-9d1a-0f6c1d7a2b36"),
                OrganizationId = OrganizationId,
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
                OrganizationId = OrganizationId,
                Provider = IntegrationProvider.GoogleCalendar,
                Purpose = "google_calendar",
                Reference = "config://GoogleCalendar:ServiceAccountJson",
            };

            var checkTool = new CompanyTool
            {
                Id = Guid.Parse("018f4f70-8b5f-7b4c-9d1a-0f6c1d7a2b40"),
                OrganizationId = OrganizationId,
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
                OrganizationId = OrganizationId,
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
                OrganizationId = OrganizationId,
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
            var handoffTool = new CompanyTool
            {
                Id = Guid.Parse("018f4f70-8b5f-7b4c-9d1a-0f6c1d7a2b44"),
                OrganizationId = OrganizationId,
                ToolKey = MvpToolKeys.RequestHumanHandoff,
                Description = "Escalate the conversation to a human agent when the customer asks for a person.",
                ParametersSchema = ParseSchema("""{"type":"object","properties":{"reason":{"type":"string"},"notes":{"type":["string","null"]}},"required":["reason","notes"],"additionalProperties":false}"""),
                IsEnabled = true,
                Configuration = ToolConfiguration.ForRequestHumanHandoff(new RequestHumanHandoffConfig
                {
                    TimeoutMinutes = 30,
                    EscalationChannel = "front-desk",
                    NotifyUsers = ["15559998888"],
                }),
            };
            DbContext.AddRange(company, profile, Channel, Customer, Conversation, InboundMessage, credential, checkTool, createTool, findTool, handoffTool);
        }

        public static async Task<ProcessorFixture> CreateAsync(string environmentName = "Testing")
        {
            var fixture = new ProcessorFixture(await PostgresWorkerDatabase.CreateAsync(), environmentName);
            await fixture.DbContext.SaveChangesAsync();
            return fixture;
        }

        public Guid OrganizationId { get; } = Guid.Parse("018f4f70-8b5f-7b4c-9d1a-0f6c1d7a2b30");

        public OrganizationContextAccessor OrganizationContext { get; }

        public CeoAgentDbContext DbContext { get; }

        public FakeMessageChannelIntegration Messaging { get; }

        public FakePaymentQrImageProvider PaymentQrImages { get; }

        public FakeAgentRuntime Agent { get; }

        public FakeCalendarIntegration Calendar { get; }

        public ProcessIncomingMessageJobProcessor Processor { get; }

        public CompanyChannel Channel { get; }

        public Customer Customer { get; }

        public Conversation Conversation { get; }

        public Message InboundMessage { get; }

        public async Task AddDefaultPaymentAccountAsync()
        {
            var bank = new Bank
            {
                Name = "Banco Uno",
                CountryCode = "CO",
                IsActive = true,
            };
            var account = new CompanyPaymentAccount
            {
                OrganizationId = OrganizationId,
                Bank = bank,
                AccountNumber = "0011223344",
                AccountType = PaymentAccountType.Ahorros,
                AccountHolderName = "Contoso Bistro",
                Currency = "COP",
                ReservationPaymentAmount = 50000m,
                QrBlobContainer = string.Empty,
                QrBlobName = string.Empty,
                IsDefault = true,
                IsActive = true,
            };
            var qrReference = BlobStorageNaming.ForPaymentQr("qr.png", account.Id);
            account.QrBlobContainer = qrReference.ContainerName;
            account.QrBlobName = qrReference.BlobName;
            DbContext.AddRange(bank, account);
            await DbContext.SaveChangesAsync();
        }

        public async Task AddAwaitingPaymentStateAsync()
        {
            DbContext.ConversationStates.Add(new ConversationState
            {
                OrganizationId = OrganizationId,
                ConversationId = Conversation.Id,
                Snapshot = new ConversationStateSnapshot
                {
                    PendingAction = "awaiting_reservation_payment_confirmation",
                    Slots =
                    [
                        new ConversationSlot { Name = "payment_account_id", TextValue = Guid.CreateVersion7().ToString("D") },
                        new ConversationSlot { Name = "amount", NumberValue = 50000m },
                        new ConversationSlot { Name = "currency", TextValue = "COP" },
                        new ConversationSlot { Name = "reservation_event_id", TextValue = "event-123" },
                        new ConversationSlot { Name = "tool_execution_id", TextValue = Guid.CreateVersion7().ToString("D") },
                    ],
                },
            });
            await DbContext.SaveChangesAsync();
        }

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

        public List<ChannelImageMessage> ImageMessages { get; } = [];

        public Exception? ThrowOnSendText { get; set; }

        public Task MarkMessageReadAsync(ChannelMessageReference message, CancellationToken cancellationToken)
        {
            ReadMessages.Add(message);
            return Task.CompletedTask;
        }

        public Task<SentMessageReference> SendTextAsync(ChannelTextMessage message, CancellationToken cancellationToken)
        {
            if (ThrowOnSendText is not null)
            {
                throw ThrowOnSendText;
            }

            TextMessages.Add(message);
            return Task.FromResult(new SentMessageReference($"sent-text-{TextMessages.Count}"));
        }

        public Task<SentMessageReference> SendImageAsync(ChannelImageMessage message, CancellationToken cancellationToken)
        {
            ImageMessages.Add(message);
            return Task.FromResult(new SentMessageReference($"sent-image-{ImageMessages.Count}"));
        }
    }

    private sealed class FakePaymentQrImageProvider : IPaymentQrImageProvider
    {
        public Task<PaymentQrImage> GetQrImageAsync(
            string blobContainer,
            string blobName,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(new PaymentQrImage(
                [1, 2, 3, 4],
                "image/png",
                "default.png"));
        }
    }

    private sealed class FakeAgentRuntime : IAgentRuntime
    {
        public List<AgentTurnRequest> Requests { get; } = [];

        public Queue<AgentTurnResult> Results { get; } = [];

        public bool CanEstimateCosts { get; set; } = true;

        public Exception? ThrowOnRun { get; set; }

        public bool CanEstimateCost(LlmProvider provider, string modelName)
        {
            return CanEstimateCosts;
        }

        public Task<AgentTurnResult> RunTurnAsync(AgentTurnRequest request, CancellationToken cancellationToken)
        {
            if (ThrowOnRun is not null)
            {
                throw ThrowOnRun;
            }

            Requests.Add(request);
            return Task.FromResult(Results.Count > 0
                ? Results.Dequeue()
                : new AgentTurnResult("Claro, reviso disponibilidad."));
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

    private sealed class FakeHostEnvironment(string environmentName) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = environmentName;

        public string ApplicationName { get; set; } = "CeoAgent.Worker.Tests";

        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    private static string? GetStringTag(Activity activity, string key)
    {
        return activity.Tags.SingleOrDefault(tag => tag.Key == key).Value;
    }

    private static int GetIntTag(Activity activity, string key)
    {
        var tagValue = activity.TagObjects.Single(tag => tag.Key == key).Value;
        return tagValue.ShouldBeOfType<int>();
    }
}
