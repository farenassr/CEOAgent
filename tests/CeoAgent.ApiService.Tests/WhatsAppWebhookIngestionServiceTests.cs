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
    public async Task IngestAsync_WhenDuplicateMessageHasNoReply_ReenqueuesExistingMessage()
    {
        var companyId = Guid.Parse("018f4f70-8b5f-7b4c-9d1a-0f6c1d7a2b30");
        await using var database = await PostgresApiDatabase.CreateAsync();
        var dbContext = database.Context;
        var queue = new FakeIncomingMessageQueue();
        var logger = new RecordingLogger<WhatsAppWebhookIngestionService>();
        var service = new WhatsAppWebhookIngestionService(dbContext, queue, TimeProvider.System, logger);
        SeedCompany(dbContext, companyId);

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
        second.Enqueued.ShouldBeTrue();
        queue.Jobs.Count.ShouldBe(2);
        first.MessageId.ShouldNotBeNull();
        second.MessageId.ShouldBe(first.MessageId);

        var parsedLog = logger.Entries.First(entry => entry.EventId.Name == "WhatsAppWebhookMessageParsed");
        parsedLog.Message.ShouldContain("PhoneNumberId=1152556904604978");
        parsedLog.Message.ShouldContain("ProviderMessageId=wamid.duplicate");
        parsedLog.Message.ShouldContain("FromLength=11");
        parsedLog.Message.ShouldContain("MessageType=text");
        parsedLog.Message.ShouldContain("TextLength=4");
    }

    [Test]
    public async Task IngestAsync_WhenDuplicateMessageAlreadyHasReply_DoesNotReenqueue()
    {
        var companyId = Guid.Parse("018f4f70-8b5f-7b4c-9d1a-0f6c1d7a2b30");
        await using var database = await PostgresApiDatabase.CreateAsync();
        var dbContext = database.Context;
        var queue = new FakeIncomingMessageQueue();
        var logger = new RecordingLogger<WhatsAppWebhookIngestionService>();
        var service = new WhatsAppWebhookIngestionService(dbContext, queue, TimeProvider.System, logger);
        SeedCompany(dbContext, companyId);

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
            CompanyId = companyId,
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
        var companyId = Guid.Parse("018f4f70-8b5f-7b4c-9d1a-0f6c1d7a2b30");
        await using var database = await PostgresApiDatabase.CreateAsync();
        var dbContext = database.Context;
        var queue = new FakeIncomingMessageQueue();
        var logger = new RecordingLogger<WhatsAppWebhookIngestionService>();
        var service = new WhatsAppWebhookIngestionService(dbContext, queue, TimeProvider.System, logger);
        SeedCompany(dbContext, companyId);

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
        var service = new WhatsAppWebhookIngestionService(database.Context, queue, TimeProvider.System, logger);

        await Should.ThrowAsync<InvalidWhatsAppWebhookPayloadException>(
            service.IngestAsync("{not valid json", "correlation-invalid", CancellationToken.None));
        queue.Jobs.ShouldBeEmpty();
    }

    [Test]
    public async Task IngestAsync_WhenPhoneNumberIdIsUnknown_ReturnsNotEnqueued()
    {
        var companyId = Guid.Parse("018f4f70-8b5f-7b4c-9d1a-0f6c1d7a2b30");
        await using var database = await PostgresApiDatabase.CreateAsync();
        var dbContext = database.Context;
        var queue = new FakeIncomingMessageQueue();
        var logger = new RecordingLogger<WhatsAppWebhookIngestionService>();
        var service = new WhatsAppWebhookIngestionService(dbContext, queue, TimeProvider.System, logger);
        SeedCompany(dbContext, companyId);

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

    private static void SeedCompany(CeoAgentDbContext dbContext, Guid companyId)
    {
        var company = new Company
        {
            Id = companyId,
            Name = "Contoso Bistro",
            TimeZoneId = "America/Bogota",
        };
        var profile = new AgentProfile
        {
            Id = Guid.Parse("018f4f70-8b5f-7b4c-9d1a-0f6c1d7a2b32"),
            CompanyId = companyId,
            ModelName = "gpt-4.1-mini",
            DisplayName = "Contoso Assistant",
            Language = "es",
        };
        var channel = CompanyChannel.ForWhatsAppCloud(
            companyId,
            "1152556904604978",
            new WhatsAppCloudMetadata
            {
                BusinessAccountId = "840790722416204",
                PhoneNumberId = "1152556904604978",
                DisplayPhoneNumber = "+15556497030",
            });

        dbContext.AddRange(company, profile, channel);
        dbContext.SaveChanges();
    }

    private sealed class FakeIncomingMessageQueue : IIncomingMessageJobEnqueuer
    {
        public List<ProcessIncomingMessageJob> Jobs { get; } = [];

        public Task EnqueueAsync(ProcessIncomingMessageJob job, CancellationToken cancellationToken)
        {
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
}
