using CeoAgent.ApiService.Modules.WhatsApp;
using CeoAgent.ApiService.Tests.Support;
using CeoAgent.Infrastructure;
using CeoAgent.Infrastructure.Entities;
using CeoAgent.Infrastructure.Entities.JsonDocuments;
using CeoAgent.Integrations.Jobs;
using CeoAgent.Shared.Enums;
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
