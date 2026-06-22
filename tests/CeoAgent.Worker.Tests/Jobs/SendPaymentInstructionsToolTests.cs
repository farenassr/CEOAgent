using System.Text.Json;
using CeoAgent.Application.Abstractions.AITools;
using CeoAgent.Application.Abstractions.Messaging;
using CeoAgent.Application.Abstractions.Payments;
using CeoAgent.Infrastructure;
using CeoAgent.Infrastructure.Entities;
using CeoAgent.Infrastructure.Entities.JsonDocuments;
using CeoAgent.Infrastructure.Implementation.AITools.Handoff;
using CeoAgent.Infrastructure.Implementation.AITools.Payments;
using CeoAgent.Infrastructure.Implementation.Messaging;
using CeoAgent.Infrastructure.Implementation.Organization;
using CeoAgent.Shared.Constants;
using CeoAgent.Shared.Enums;
using CeoAgent.Shared.Messaging;
using CeoAgent.Shared.Payment;
using CeoAgent.Shared.Storage;
using CeoAgent.Worker.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;

namespace CeoAgent.Worker.Tests.Jobs;

public sealed class SendPaymentInstructionsToolTests
{
    [Test]
    public async Task ExecuteAsync_WithSuccessfulReservation_SendsQrImageWithCompleteCaption()
    {
        await using var fixture = await PaymentToolFixture.CreateAsync();
        await fixture.AddDefaultPaymentAccountAsync();
        var reservation = await fixture.AddSuccessfulReservationExecutionAsync();

        var execution = (ToolExecution)await fixture.Tool.ExecuteAsync(
            fixture.CreatePaymentExecutionContext("send-payment-key"),
            new SendPaymentInstructionsRequest(),
            CancellationToken.None);

        execution.Status.ShouldBe(ToolExecutionStatus.ToolExecutionSucceeded);
        execution.FailureReason.ShouldBeNull();
        execution.Request!.ToolKey.ShouldBe(MvpToolKeys.SendPaymentInstructions);
        execution.Result!.SendPaymentInstructions.ShouldNotBeNull();
        execution.Result.SendPaymentInstructions.PaymentInstructionsSent.ShouldBeTrue();
        execution.Result.SendPaymentInstructions.CustomerVisibleMessageSent.ShouldBeTrue();
        execution.Result.SendPaymentInstructions.HandoffRequested.ShouldBeTrue();
        execution.Result.SendPaymentInstructions.ReservationEventId.ShouldBe("event-123");

        var image = fixture.Messaging.ImageMessages.Single();
        image.IdempotencyKey.ShouldBe($"payment:{reservation.Id}");
        image.Caption.ShouldContain("¡Tu reserva ha sido creada!");
        image.Caption.ShouldContain("Reservation for 4");
        image.Caption.ShouldContain("Ada Lovelace");
        image.Caption.ShouldContain("2026-05-28 4:00 pm -05:00");
        image.Caption.ShouldContain("0011223344");
        image.Caption.ShouldContain("QR");
        image.Caption.ShouldContain("50000 COP");
        image.Caption.ShouldContain("consumible");
        image.Caption.ShouldContain("confirmada al recibir el pago");

        var paymentMessage = await fixture.DbContext.Messages.SingleAsync(message =>
            message.ProviderMessageId == $"payment:{reservation.Id}");
        execution.Result.SendPaymentInstructions.PaymentMessageId.ShouldBe(paymentMessage.Id);
        paymentMessage.Payload.ShouldNotBeNull();
        paymentMessage.Payload.BlobUri.ShouldBe(fixture.PaymentAccountBlobUri);

        await fixture.DbContext.Entry(fixture.Conversation).ReloadAsync();
        fixture.Conversation.Status.ShouldBe(ConversationStatus.HandedOff);
        (await fixture.DbContext.ToolExecutions.CountAsync(toolExecution =>
            toolExecution.ToolKey == MvpToolKeys.RequestHumanHandoff)).ShouldBe(1);
    }

    [Test]
    public async Task ExecuteAsync_WithSameIdempotencyKey_DoesNotSendDuplicatePaymentImage()
    {
        await using var fixture = await PaymentToolFixture.CreateAsync();
        await fixture.AddDefaultPaymentAccountAsync();
        await fixture.AddSuccessfulReservationExecutionAsync();
        var context = fixture.CreatePaymentExecutionContext("send-payment-key");

        _ = await fixture.Tool.ExecuteAsync(
            context,
            new SendPaymentInstructionsRequest(),
            CancellationToken.None);
        _ = await fixture.Tool.ExecuteAsync(
            context,
            new SendPaymentInstructionsRequest(),
            CancellationToken.None);

        fixture.Messaging.ImageMessages.Count.ShouldBe(1);
        (await fixture.DbContext.ToolExecutions.CountAsync(execution =>
            execution.ToolKey == MvpToolKeys.SendPaymentInstructions)).ShouldBe(1);
        (await fixture.DbContext.ToolExecutions.CountAsync(execution =>
            execution.ToolKey == MvpToolKeys.RequestHumanHandoff)).ShouldBe(1);
        (await fixture.DbContext.Messages.CountAsync(message =>
            message.ProviderMessageId != null
            && EF.Functions.Like(message.ProviderMessageId, "payment:%"))).ShouldBe(1);
    }

    [Test]
    public async Task ExecuteAsync_WithoutSuccessfulReservation_ReturnsReservationNotFound()
    {
        await using var fixture = await PaymentToolFixture.CreateAsync();
        await fixture.AddDefaultPaymentAccountAsync();

        var execution = (ToolExecution)await fixture.Tool.ExecuteAsync(
            fixture.CreatePaymentExecutionContext("send-payment-key"),
            new SendPaymentInstructionsRequest(),
            CancellationToken.None);

        execution.Status.ShouldBe(ToolExecutionStatus.ToolExecutionDenied);
        execution.FailureReason.ShouldBe("reservation_not_found");
        execution.Result!.SendPaymentInstructions.ShouldNotBeNull();
        execution.Result.SendPaymentInstructions.PaymentInstructionsSent.ShouldBeFalse();
        execution.Result.SendPaymentInstructions.CustomerVisibleMessageSent.ShouldBeFalse();
        execution.Result.SendPaymentInstructions.HandoffRequested.ShouldBeFalse();
        execution.Result.SendPaymentInstructions.PaymentMessageId.ShouldBeNull();
        fixture.Messaging.ImageMessages.ShouldBeEmpty();
        fixture.Messaging.TextMessages.ShouldBeEmpty();
    }

    [Test]
    public async Task ExecuteAsync_WithoutConfiguredPaymentAccount_SendsFallbackAndHandsOff()
    {
        await using var fixture = await PaymentToolFixture.CreateAsync();
        await fixture.AddSuccessfulReservationExecutionAsync();

        var execution = (ToolExecution)await fixture.Tool.ExecuteAsync(
            fixture.CreatePaymentExecutionContext("send-payment-key"),
            new SendPaymentInstructionsRequest(),
            CancellationToken.None);

        execution.Status.ShouldBe(ToolExecutionStatus.ToolExecutionFailed);
        execution.FailureReason.ShouldBe("payment_account_not_configured");
        execution.Result!.SendPaymentInstructions.ShouldNotBeNull();
        execution.Result.SendPaymentInstructions.PaymentInstructionsSent.ShouldBeFalse();
        execution.Result.SendPaymentInstructions.CustomerVisibleMessageSent.ShouldBeTrue();
        execution.Result.SendPaymentInstructions.HandoffRequested.ShouldBeTrue();

        fixture.Messaging.ImageMessages.ShouldBeEmpty();
        fixture.Messaging.TextMessages.ShouldContain(message =>
            message.RecipientExternalId == fixture.Customer.ExternalCustomerId
            && message.Text.Contains("persona del equipo", StringComparison.OrdinalIgnoreCase));

        await fixture.DbContext.Entry(fixture.Conversation).ReloadAsync();
        fixture.Conversation.Status.ShouldBe(ConversationStatus.HandedOff);
        (await fixture.DbContext.ToolExecutions.CountAsync(toolExecution =>
            toolExecution.ToolKey == MvpToolKeys.RequestHumanHandoff)).ShouldBe(1);
    }

    [Test]
    public async Task ExecuteAsync_WhenQrImageSendFails_DoesNotRequestHandoff()
    {
        await using var fixture = await PaymentToolFixture.CreateAsync();
        await fixture.AddDefaultPaymentAccountAsync();
        await fixture.AddSuccessfulReservationExecutionAsync();
        fixture.PaymentQrImages.ThrowOnGetQr = new InvalidOperationException("blob unavailable");

        var execution = (ToolExecution)await fixture.Tool.ExecuteAsync(
            fixture.CreatePaymentExecutionContext("send-payment-key"),
            new SendPaymentInstructionsRequest(),
            CancellationToken.None);

        execution.Status.ShouldBe(ToolExecutionStatus.ToolExecutionFailed);
        execution.FailureReason.ShouldBe("payment_qr_send_failed");
        execution.Result!.SendPaymentInstructions.ShouldNotBeNull();
        execution.Result.SendPaymentInstructions.PaymentInstructionsSent.ShouldBeFalse();
        execution.Result.SendPaymentInstructions.CustomerVisibleMessageSent.ShouldBeFalse();
        execution.Result.SendPaymentInstructions.HandoffRequested.ShouldBeFalse();
        fixture.Messaging.ImageMessages.ShouldBeEmpty();
        fixture.Messaging.TextMessages.ShouldBeEmpty();

        await fixture.DbContext.Entry(fixture.Conversation).ReloadAsync();
        fixture.Conversation.Status.ShouldBe(ConversationStatus.Open);
        (await fixture.DbContext.ToolExecutions.CountAsync(toolExecution =>
            toolExecution.ToolKey == MvpToolKeys.RequestHumanHandoff)).ShouldBe(0);
    }

    [Test]
    public async Task ParametersSchema_ForSendPaymentInstructions_HasNoArguments()
    {
        await using var fixture = await PaymentToolFixture.CreateAsync();

        var schema = fixture.Tool.ParametersSchema;

        schema.GetProperty("type").GetString().ShouldBe("object");
        schema.GetProperty("properties").EnumerateObject().ShouldBeEmpty();
        schema.GetProperty("required").EnumerateArray().ShouldBeEmpty();
        schema.GetProperty("additionalProperties").GetBoolean().ShouldBeFalse();
    }

    private sealed class PaymentToolFixture : IAsyncDisposable
    {
        private readonly PostgresWorkerDatabase database;

        private PaymentToolFixture(PostgresWorkerDatabase database)
        {
            this.database = database;
            OrganizationContext = database.OrganizationContext;
            OrganizationContext.SetOrganization(OrganizationId);
            DbContext = database.Context;
            Messaging = new FakeMessageChannelIntegration();
            PaymentQrImages = new FakePaymentQrImageProvider();
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
            var dataReader = new PaymentInstructionDataReader(DbContext);
            var dispatchService = new PaymentInstructionDispatchService(
                DbContext,
                outboundMessageDispatcher,
                PaymentQrImages,
                TimeProvider.System,
                NullLogger<PaymentInstructionDispatchService>.Instance);
            var sender = new ReservationPaymentInstructionSender(
                DbContext,
                dataReader,
                dispatchService,
                handoffExecutor,
                TimeProvider.System);
            Tool = new SendPaymentInstructionsTool(sender);

            var company = new Company
            {
                Id = OrganizationId,
                Name = "Contoso Bistro",
                TimeZoneId = "America/Bogota",
            };
            var profile = new AgentProfile
            {
                Id = Guid.Parse("018f4f70-8b5f-7b4c-9d1a-0f6c1d7a2b32"),
                OrganizationId = OrganizationId,
                ModelName = "gpt-4.1-mini",
                DisplayName = "Contoso Assistant",
                Language = "es",
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
            TriggerMessage = new Message
            {
                Id = Guid.Parse("018f4f70-8b5f-7b4c-9d1a-0f6c1d7a2b36"),
                OrganizationId = OrganizationId,
                ConversationId = Conversation.Id,
                Role = MessageRole.ToolCall,
                Type = MessageType.Text,
                MessageText = MvpToolKeys.SendPaymentInstructions,
                OccurredAt = new DateTime(2026, 5, 28, 21, 0, 0, DateTimeKind.Utc),
            };

            CreateReservationTool = new CompanyTool
            {
                Id = Guid.Parse("018f4f70-8b5f-7b4c-9d1a-0f6c1d7a2b40"),
                OrganizationId = OrganizationId,
                ToolKey = MvpToolKeys.CreateGoogleCalendarReservation,
                IsEnabled = true,
            };
            PaymentTool = new CompanyTool
            {
                Id = Guid.Parse("018f4f70-8b5f-7b4c-9d1a-0f6c1d7a2b41"),
                OrganizationId = OrganizationId,
                ToolKey = MvpToolKeys.SendPaymentInstructions,
                Description = "Send reservation payment instructions.",
                ParametersSchema = ParseSchema("""{"type":"object","properties":{},"required":[],"additionalProperties":false}"""),
                IsEnabled = true,
            };
            HandoffTool = new CompanyTool
            {
                Id = Guid.Parse("018f4f70-8b5f-7b4c-9d1a-0f6c1d7a2b44"),
                OrganizationId = OrganizationId,
                ToolKey = MvpToolKeys.RequestHumanHandoff,
                IsEnabled = true,
                Configuration = ToolConfiguration.ForRequestHumanHandoff(new RequestHumanHandoffConfig
                {
                    TimeoutMinutes = 30,
                    EscalationChannel = "front-desk",
                    NotifyUsers = ["15559998888"],
                }),
            };

            DbContext.AddRange(
                company,
                profile,
                Channel,
                Customer,
                Conversation,
                TriggerMessage,
                CreateReservationTool,
                PaymentTool,
                HandoffTool);
        }

        public static async Task<PaymentToolFixture> CreateAsync()
        {
            var fixture = new PaymentToolFixture(await PostgresWorkerDatabase.CreateAsync());
            await fixture.DbContext.SaveChangesAsync();
            return fixture;
        }

        public Guid OrganizationId { get; } = Guid.Parse("018f4f70-8b5f-7b4c-9d1a-0f6c1d7a2b30");

        public CeoAgentDbContext DbContext { get; }

        public FakeMessageChannelIntegration Messaging { get; }

        public FakePaymentQrImageProvider PaymentQrImages { get; }

        public SendPaymentInstructionsTool Tool { get; }

        public CompanyChannel Channel { get; }

        public Customer Customer { get; }

        public Conversation Conversation { get; }

        public Message TriggerMessage { get; }

        public CompanyTool CreateReservationTool { get; }

        public CompanyTool PaymentTool { get; }

        public CompanyTool HandoffTool { get; }

        public OrganizationContextAccessor OrganizationContext { get; }

        public string PaymentAccountBlobUri { get; } =
            "https://storage.test/private/organizations/contoso-bistro/payments/payment-accounts/default/qr.png";

        public ToolExecutionContext CreatePaymentExecutionContext(string idempotencyKey)
        {
            return new ToolExecutionContext(
                OrganizationId,
                Conversation.Id,
                PaymentTool.Id,
                TriggerMessage.Id,
                idempotencyKey);
        }

        public async Task<ToolExecution> AddSuccessfulReservationExecutionAsync()
        {
            var execution = new ToolExecution
            {
                OrganizationId = OrganizationId,
                ConversationId = Conversation.Id,
                CompanyToolId = CreateReservationTool.Id,
                TriggerMessageId = TriggerMessage.Id,
                ToolKey = MvpToolKeys.CreateGoogleCalendarReservation,
                IdempotencyKey = $"{Conversation.Id:N}:{TriggerMessage.Id:N}:{MvpToolKeys.CreateGoogleCalendarReservation}:reservation",
                Status = ToolExecutionStatus.ToolExecutionSucceeded,
                Request = ToolExecutionRequest.ForCreateGoogleCalendarReservation(new CreateCalendarEventRequest
                {
                    Start = new DateTimeOffset(2026, 5, 28, 16, 0, 0, TimeSpan.FromHours(-5)),
                    End = new DateTimeOffset(2026, 5, 28, 17, 0, 0, TimeSpan.FromHours(-5)),
                    Summary = "Reservation for 4",
                    CustomerName = "Ada Lovelace",
                }),
                Result = ToolExecutionResult.ForCreateGoogleCalendarReservation(new CreateCalendarEventResult
                {
                    EventId = "event-123",
                    EventUrl = "https://calendar.google.com/event?eid=event-123",
                }),
            };
            DbContext.ToolExecutions.Add(execution);
            await DbContext.SaveChangesAsync();
            return execution;
        }

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
            account.QrBlobUri = PaymentAccountBlobUri;
            DbContext.AddRange(bank, account);
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

        public Task<SentMessageReference> SendImageAsync(ChannelImageMessage message, CancellationToken cancellationToken)
        {
            ImageMessages.Add(message);
            return Task.FromResult(new SentMessageReference($"sent-image-{ImageMessages.Count}"));
        }
    }

    private sealed class FakePaymentQrImageProvider : IPaymentQrImageProvider
    {
        public Exception? ThrowOnGetQr { get; set; }

        public Task<PaymentQrImage> GetQrImageAsync(
            string blobContainer,
            string blobName,
            CancellationToken cancellationToken)
        {
            if (ThrowOnGetQr is not null)
            {
                throw ThrowOnGetQr;
            }

            return Task.FromResult(new PaymentQrImage(
                [1, 2, 3, 4],
                "image/png",
                "default.png"));
        }
    }
}
