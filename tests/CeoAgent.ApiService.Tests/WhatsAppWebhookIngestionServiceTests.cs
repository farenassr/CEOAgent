using CeoAgent.ApiService.Modules.WhatsApp;
using CeoAgent.ApiService.Tests.Support;
using CeoAgent.Infrastructure;
using CeoAgent.Infrastructure.Entities;
using CeoAgent.Infrastructure.Entities.JsonDocuments;
using CeoAgent.Application.Abstractions.Jobs;
using CeoAgent.Shared.Jobs;
using CeoAgent.Shared.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Shouldly;

namespace CeoAgent.ApiService.Tests;

public sealed class WhatsAppWebhookIngestionServiceTests
{
    [Test]
    public async Task IngestAsync_WhenInitialQueueEnqueueFails_PersistsOutboxAndDispatchesLater()
    {
        var organizationId = Guid.Parse("018f4f70-8b5f-7b4c-9d1a-0f6c1d7a2b30");
        await using var database = await PostgresApiDatabase.CreateAsync();
        var dbContext = database.Context;
        var queue = new FakeIncomingMessageQueue { FailNextEnqueue = true };
        var logger = new RecordingLogger<WhatsAppWebhookIngestionService>();
        var dispatcherLogger = new RecordingLogger<IncomingMessageOutboxDispatcher>();
        var dispatcher = new IncomingMessageOutboxDispatcher(dbContext, queue, TimeProvider.System, dispatcherLogger);
        var service = new WhatsAppWebhookIngestionService(dbContext, dispatcher, TimeProvider.System, logger);
        await SeedCompanyAsync(dbContext, organizationId);

        const string webhookJson = """
            {
              "entry": [
                {
                  "changes": [
                    {
                      "value": {
                        "metadata": {
                          "phone_number_id": "1152556904604978"
                        },
                        "contacts": [
                          {
                            "wa_id": "15551234567",
                            "profile": { "name": "Ada" }
                          }
                        ],
                        "messages": [
                          {
                            "id": "wamid.enqueue-fails",
                            "from": "15551234567",
                            "timestamp": "1779987600",
                            "type": "text",
                            "text": { "body": "Hola" }
                          }
                        ]
                      }
                    }
                  ]
                }
              ]
            }
            """;

        var result = await service.IngestAsync(webhookJson, "correlation-fail", CancellationToken.None);

        result.Enqueued.ShouldBeFalse();
        result.MessageId.ShouldNotBeNull();
        queue.Jobs.ShouldBeEmpty();
        var pendingOutbox = await dbContext.IncomingMessageOutbox
            .IgnoreQueryFilters()
            .SingleAsync(row => row.MessageId == result.MessageId.Value);
        pendingOutbox.Status.ShouldBe(IncomingMessageOutboxStatus.QueueDispatchRetryScheduled);
        pendingOutbox.AttemptCount.ShouldBe(1);
        dispatcherLogger.Entries.ShouldContain(entry =>
            entry.EventId.Id == 2102
            && entry.EventId.Name == "IncomingMessageOutboxDispatchFailed"
            && entry.Message.Contains("AttemptCount=1", StringComparison.Ordinal));

        var dispatched = await dispatcher.DispatchPendingAsync(10, CancellationToken.None);

        dispatched.ShouldBe(1);
        queue.Jobs.Count.ShouldBe(1);
        queue.Jobs[0].MessageId.ShouldBe(result.MessageId.Value);
        pendingOutbox.Status.ShouldBe(IncomingMessageOutboxStatus.QueuedForWorkerProcessing);
        pendingOutbox.AttemptCount.ShouldBe(2);
        dispatcherLogger.Entries.ShouldContain(entry =>
            entry.EventId.Id == 2101
            && entry.EventId.Name == "IncomingMessageOutboxDispatchSucceeded"
            && entry.Message.Contains("AttemptCount=2", StringComparison.Ordinal));
    }

    [Test]
    public async Task DispatchPendingAsync_WhenInProgressClaimIsStale_ReclaimsAndDispatches()
    {
        var organizationId = Guid.Parse("018f4f70-8b5f-7b4c-9d1a-0f6c1d7a2b30");
        await using var database = await PostgresApiDatabase.CreateAsync();
        var dbContext = database.Context;
        database.OrganizationContext.SetOrganization(organizationId);
        await SeedCompanyAsync(dbContext, organizationId);
        var message = await SeedConversationMessageAsync(dbContext, organizationId);
        var clock = new FixedTimeProvider(new DateTimeOffset(2026, 6, 18, 12, 0, 0, TimeSpan.Zero));
        var staleClaimedAt = clock.GetUtcNow().AddMinutes(-10).UtcDateTime;
        var outbox = new IncomingMessageOutbox
        {
            OrganizationId = organizationId,
            ConversationId = message.ConversationId,
            MessageId = message.Id,
            Status = IncomingMessageOutboxStatus.QueueDispatchInProgress,
            AttemptCount = 1,
            LastAttemptAt = staleClaimedAt,
            ClaimedAt = staleClaimedAt,
            ClaimedBy = "dead-dispatcher",
            MaxAttempts = 5,
        };
        dbContext.IncomingMessageOutbox.Add(outbox);
        await dbContext.SaveChangesAsync();
        var queue = new FakeIncomingMessageQueue();
        var dispatcher = new IncomingMessageOutboxDispatcher(
            dbContext,
            queue,
            clock,
            new RecordingLogger<IncomingMessageOutboxDispatcher>());

        var dispatched = await dispatcher.DispatchPendingAsync(10, CancellationToken.None);

        dispatched.ShouldBe(1);
        queue.Jobs.Count.ShouldBe(1);
        queue.Jobs[0].MessageId.ShouldBe(message.Id);
        outbox.Status.ShouldBe(IncomingMessageOutboxStatus.QueuedForWorkerProcessing);
        outbox.AttemptCount.ShouldBe(2);
        outbox.ClaimedBy.ShouldNotBe("dead-dispatcher");
    }

    [Test]
    public async Task DispatchPendingAsync_WhenInProgressClaimIsFresh_DoesNotReclaim()
    {
        var organizationId = Guid.Parse("018f4f70-8b5f-7b4c-9d1a-0f6c1d7a2b30");
        await using var database = await PostgresApiDatabase.CreateAsync();
        var dbContext = database.Context;
        database.OrganizationContext.SetOrganization(organizationId);
        await SeedCompanyAsync(dbContext, organizationId);
        var message = await SeedConversationMessageAsync(dbContext, organizationId);
        var clock = new FixedTimeProvider(new DateTimeOffset(2026, 6, 18, 12, 0, 0, TimeSpan.Zero));
        var outbox = new IncomingMessageOutbox
        {
            OrganizationId = organizationId,
            ConversationId = message.ConversationId,
            MessageId = message.Id,
            Status = IncomingMessageOutboxStatus.QueueDispatchInProgress,
            AttemptCount = 1,
            LastAttemptAt = clock.GetUtcNow().AddMinutes(-1).UtcDateTime,
            ClaimedAt = clock.GetUtcNow().AddMinutes(-1).UtcDateTime,
            ClaimedBy = "active-dispatcher",
            MaxAttempts = 5,
        };
        dbContext.IncomingMessageOutbox.Add(outbox);
        await dbContext.SaveChangesAsync();
        var queue = new FakeIncomingMessageQueue();
        var dispatcher = new IncomingMessageOutboxDispatcher(
            dbContext,
            queue,
            clock,
            new RecordingLogger<IncomingMessageOutboxDispatcher>());

        var dispatched = await dispatcher.DispatchPendingAsync(10, CancellationToken.None);

        dispatched.ShouldBe(0);
        queue.Jobs.ShouldBeEmpty();
        outbox.Status.ShouldBe(IncomingMessageOutboxStatus.QueueDispatchInProgress);
        outbox.AttemptCount.ShouldBe(1);
        outbox.ClaimedBy.ShouldBe("active-dispatcher");
    }

    [Test]
    public async Task IngestAsync_WhenDuplicateMessageHasNoReplyAndOutboxAlreadyDispatched_DoesNotReenqueueExistingMessage()
    {
        var organizationId = Guid.Parse("018f4f70-8b5f-7b4c-9d1a-0f6c1d7a2b30");
        await using var database = await PostgresApiDatabase.CreateAsync();
        var dbContext = database.Context;
        var queue = new FakeIncomingMessageQueue();
        var logger = new RecordingLogger<WhatsAppWebhookIngestionService>();
        var service = CreateService(dbContext, queue, logger);
        await SeedCompanyAsync(dbContext, organizationId);

        const string webhookJson = """
            {
              "entry": [
                {
                  "changes": [
                    {
                      "value": {
                        "metadata": {
                          "phone_number_id": "1152556904604978"
                        },
                        "contacts": [
                          {
                            "wa_id": "15551234567",
                            "profile": { "name": "Ada" }
                          }
                        ],
                        "messages": [
                          {
                            "id": "wamid.duplicate",
                            "from": "15551234567",
                            "timestamp": "1779987600",
                            "type": "text",
                            "text": { "body": "Hola" }
                          }
                        ]
                      }
                    }
                  ]
                }
              ]
            }
            """;

        var first = await service.IngestAsync(webhookJson, "correlation-1", CancellationToken.None);
        var second = await service.IngestAsync(webhookJson, "correlation-2", CancellationToken.None);

        first.Enqueued.ShouldBeTrue();
        second.Enqueued.ShouldBeFalse();
        queue.Jobs.Count.ShouldBe(1);
        first.MessageId.ShouldNotBeNull();
        second.MessageId.ShouldBe(first.MessageId);

        var parsedLog = logger.Entries.First(entry => entry.EventId.Name == "WhatsAppWebhookMessageParsed");
        parsedLog.Message.ShouldContain("PhoneNumberId=1152556904604978");
        parsedLog.Message.ShouldContain("ProviderMessageId=wamid.duplicate");
        parsedLog.Message.ShouldContain("FromLength=11");
        parsedLog.Message.ShouldContain("MessageType=text");
        parsedLog.Message.ShouldContain("TextLength=4");
        logger.Entries.ShouldContain(entry =>
            entry.EventId.Id == 4202
            && entry.EventId.Name == "WhatsAppWebhookMessagePersisted"
            && entry.Message.Contains("ProviderMessageId=wamid.duplicate", StringComparison.Ordinal));
        logger.Entries.ShouldContain(entry =>
            entry.EventId.Id == 4203
            && entry.EventId.Name == "WhatsAppWebhookMessageEnqueued"
            && entry.Message.Contains("ProviderMessageId=wamid.duplicate", StringComparison.Ordinal));
    }

    [Test]
    public async Task IngestAsync_WhenDuplicateMessageAlreadyHasReply_DoesNotReenqueue()
    {
        var organizationId = Guid.Parse("018f4f70-8b5f-7b4c-9d1a-0f6c1d7a2b30");
        await using var database = await PostgresApiDatabase.CreateAsync();
        var dbContext = database.Context;
        var queue = new FakeIncomingMessageQueue();
        var logger = new RecordingLogger<WhatsAppWebhookIngestionService>();
        var service = CreateService(dbContext, queue, logger);
        await SeedCompanyAsync(dbContext, organizationId);

        const string webhookJson = """
            {
              "entry": [
                {
                  "changes": [
                    {
                      "value": {
                        "metadata": {
                          "phone_number_id": "1152556904604978"
                        },
                        "contacts": [
                          {
                            "wa_id": "15551234567",
                            "profile": { "name": "Ada" }
                          }
                        ],
                        "messages": [
                          {
                            "id": "wamid.duplicate-with-reply",
                            "from": "15551234567",
                            "timestamp": "1779987600",
                            "type": "text",
                            "text": { "body": "Hola" }
                          }
                        ]
                      }
                    }
                  ]
                }
              ]
            }
            """;

        var first = await service.IngestAsync(webhookJson, "correlation-1", CancellationToken.None);
        first.MessageId.ShouldNotBeNull();
        dbContext.Messages.Add(new Message
        {
            OrganizationId = organizationId,
            ConversationId = first.ConversationId!.Value,
            Role = MessageRole.Assistant,
            Type = MessageType.Text,
            MessageText = "Hola",
            ProviderMessageId = $"reply:{first.MessageId.Value}",
            OccurredAt = TimeProvider.System.GetUtcNow().UtcDateTime,
        });
        await dbContext.SaveChangesAsync();

        var second = await service.IngestAsync(webhookJson, "correlation-2", CancellationToken.None);

        second.Enqueued.ShouldBeFalse();
        second.MessageId.ShouldBe(first.MessageId);
        queue.Jobs.Count.ShouldBe(1);
    }

    [Test]
    public async Task IngestAsync_WhenWebhookContainsMultipleMessages_PersistsAndEnqueuesEachMessage()
    {
        var organizationId = Guid.Parse("018f4f70-8b5f-7b4c-9d1a-0f6c1d7a2b30");
        await using var database = await PostgresApiDatabase.CreateAsync();
        var dbContext = database.Context;
        var queue = new FakeIncomingMessageQueue();
        var logger = new RecordingLogger<WhatsAppWebhookIngestionService>();
        var service = CreateService(dbContext, queue, logger);
        await SeedCompanyAsync(dbContext, organizationId);

        const string webhookJson = """
            {
              "entry": [
                {
                  "changes": [
                    {
                      "value": {
                        "metadata": { "phone_number_id": "1152556904604978" },
                        "messages": [
                          {
                            "id": "wamid.batch-1",
                            "from": "15551234567",
                            "timestamp": "1779987600",
                            "type": "text",
                            "text": { "body": "Hola" }
                          },
                          {
                            "id": "wamid.batch-2",
                            "from": "15557654321",
                            "timestamp": "1779987660",
                            "type": "text",
                            "text": { "body": "Mesa para dos" }
                          }
                        ]
                      }
                    }
                  ]
                }
              ]
            }
            """;

        var result = await service.IngestAsync(webhookJson, "correlation-batch", CancellationToken.None);

        result.Enqueued.ShouldBeTrue();
        queue.Jobs.Count.ShouldBe(2);
        dbContext.Messages.IgnoreQueryFilters().Count(message => message.ProviderMessageId != null && message.ProviderMessageId.StartsWith("wamid.batch")).ShouldBe(2);
    }

    [Test]
    public async Task IngestAsync_WhenJsonIsInvalid_ThrowsAndDoesNotEnqueue()
    {
        await using var database = await PostgresApiDatabase.CreateAsync();
        var queue = new FakeIncomingMessageQueue();
        var logger = new RecordingLogger<WhatsAppWebhookIngestionService>();
        var service = CreateService(database.Context, queue, logger);

        await Should.ThrowAsync<InvalidWhatsAppWebhookPayloadException>(
            service.IngestAsync("{not valid json", "correlation-invalid", CancellationToken.None));
        queue.Jobs.ShouldBeEmpty();
    }

    [Test]
    public async Task IngestAsync_WhenPhoneNumberIdIsUnknown_ReturnsNotEnqueued()
    {
        var organizationId = Guid.Parse("018f4f70-8b5f-7b4c-9d1a-0f6c1d7a2b30");
        await using var database = await PostgresApiDatabase.CreateAsync();
        var dbContext = database.Context;
        var queue = new FakeIncomingMessageQueue();
        var logger = new RecordingLogger<WhatsAppWebhookIngestionService>();
        var service = CreateService(dbContext, queue, logger);
        await SeedCompanyAsync(dbContext, organizationId);

        const string webhookJson = """
            {
              "entry": [
                {
                  "changes": [
                    {
                      "value": {
                        "metadata": { "phone_number_id": "unknown-phone-number" },
                        "messages": [
                          {
                            "id": "wamid.unknown-channel",
                            "from": "15551234567",
                            "timestamp": "1779987600",
                            "type": "text",
                            "text": { "body": "Hola" }
                          }
                        ]
                      }
                    }
                  ]
                }
              ]
            }
            """;

        var result = await service.IngestAsync(webhookJson, "correlation-unknown", CancellationToken.None);

        result.Enqueued.ShouldBeFalse();
        queue.Jobs.ShouldBeEmpty();
    }

    private static async Task SeedCompanyAsync(CeoAgentDbContext dbContext, Guid organizationId)
    {
        var company = new Company
        {
            Id = organizationId,
            Name = "Contoso Bistro",
            TimeZoneId = "America/Bogota",
        };
        var profile = new AgentProfile
        {
            Id = Guid.Parse("018f4f70-8b5f-7b4c-9d1a-0f6c1d7a2b32"),
            OrganizationId = organizationId,
            ModelName = "gpt-4.1-mini",
            DisplayName = "Contoso Assistant",
            Language = "es",
        };
        var channel = CompanyChannel.ForWhatsAppCloud(
            organizationId,
            "1152556904604978",
            new WhatsAppCloudMetadata
            {
                BusinessAccountId = "840790722416204",
                PhoneNumberId = "1152556904604978",
                DisplayPhoneNumber = "+15556497030",
            });

        dbContext.AddRange(company, profile, channel);
        await dbContext.SaveChangesAsync();
    }

    private static async Task<Message> SeedConversationMessageAsync(CeoAgentDbContext dbContext, Guid organizationId)
    {
        var customer = new Customer
        {
            Id = Guid.Parse("018f4f70-8b5f-7b4c-9d1a-0f6c1d7a2b33"),
            OrganizationId = organizationId,
            CompanyChannelId = dbContext.CompanyChannels.Local.Single().Id,
            ExternalCustomerId = "15551234567",
        };
        var conversation = new Conversation
        {
            Id = Guid.Parse("018f4f70-8b5f-7b4c-9d1a-0f6c1d7a2b34"),
            OrganizationId = organizationId,
            Customer = customer,
            CompanyChannelId = customer.CompanyChannelId,
            AgentProfileId = dbContext.AgentProfiles.Local.Single().Id,
            LastMessageAt = new DateTime(2026, 6, 18, 12, 0, 0, DateTimeKind.Utc),
        };
        var message = new Message
        {
            Id = Guid.Parse("018f4f70-8b5f-7b4c-9d1a-0f6c1d7a2b35"),
            OrganizationId = organizationId,
            Conversation = conversation,
            Role = MessageRole.User,
            Type = MessageType.Text,
            MessageText = "Hola",
            ProviderMessageId = "wamid.stale-claim",
            OccurredAt = new DateTime(2026, 6, 18, 12, 0, 0, DateTimeKind.Utc),
        };

        dbContext.AddRange(customer, conversation, message);
        await dbContext.SaveChangesAsync();
        return message;
    }

    private static WhatsAppWebhookIngestionService CreateService(
        CeoAgentDbContext dbContext,
        FakeIncomingMessageQueue queue,
        RecordingLogger<WhatsAppWebhookIngestionService> logger)
    {
        var dispatcherLogger = new RecordingLogger<IncomingMessageOutboxDispatcher>();
        var dispatcher = new IncomingMessageOutboxDispatcher(dbContext, queue, TimeProvider.System, dispatcherLogger);
        return new WhatsAppWebhookIngestionService(dbContext, dispatcher, TimeProvider.System, logger);
    }

    private sealed class FakeIncomingMessageQueue : IIncomingMessageJobEnqueuer
    {
        public List<ProcessIncomingMessageJob> Jobs { get; } = [];

        public bool FailNextEnqueue { get; set; }

        public Task EnqueueAsync(ProcessIncomingMessageJob job, CancellationToken cancellationToken)
        {
            if (FailNextEnqueue)
            {
                FailNextEnqueue = false;
                throw new InvalidOperationException("Simulated queue outage.");
            }

            Jobs.Add(job);
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingLogger<T> : ILogger<T>
    {
        public List<LogEntry> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull
        {
            return null;
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return true;
        }

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            Entries.Add(new LogEntry(logLevel, eventId, formatter(state, exception)));
        }
    }

    private sealed record LogEntry(LogLevel LogLevel, EventId EventId, string Message);

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow()
        {
            return utcNow;
        }
    }
}
