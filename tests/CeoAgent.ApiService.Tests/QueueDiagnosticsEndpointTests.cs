using System.Net;
using System.Net.Http.Json;
using CeoAgent.ApiService.Infrastructure.Queues.Abstractions;
using CeoAgent.ApiService.Infrastructure.Queues.Contracts;
using CeoAgent.ApiService.Tests.Support;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Shouldly;

namespace CeoAgent.ApiService.Tests;

[NotInParallel]
public sealed class QueueDiagnosticsEndpointTests
{
    [Test]
    public async Task PostMessage_AddsMessageToRequestedQueue()
    {
        var queueDiagnostics = new FakeQueueDiagnosticsService();
        const string adminKey = "test-admin-key";
        await using var factory = new ApiFactory(configureServices: services =>
        {
            services.RemoveAll<IQueueDiagnosticsService>();
            services.AddSingleton<IQueueDiagnosticsService>(queueDiagnostics);
            services.Configure<CeoAgent.ApiService.Infrastructure.Security.AdminApiKeyOptions>(options =>
            {
                options.Key = adminKey;
            });
        });
        using var client = factory.CreateClient();

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            "/v1/admin/queues/process-incoming-message/messages")
        {
            Content = JsonContent.Create(new { messageText = "{\"hello\":\"queue\"}" }),
        };
        request.Headers.Add("X-Admin-Api-Key", adminKey);

        using var response = await client.SendAsync(request);
        var body = await response.Content.ReadFromJsonAsync<QueueMessageEnqueuedResponse>();

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        body.ShouldNotBeNull();
        body.QueueName.ShouldBe("process-incoming-message");
        body.MessageId.ShouldBe("message-1");
        queueDiagnostics.SentMessages.Single().QueueName.ShouldBe("process-incoming-message");
        queueDiagnostics.SentMessages.Single().MessageText.ShouldBe("{\"hello\":\"queue\"}");
    }

    [Test]
    public async Task GetQueues_ReturnsQueuesWithPeekedMessages()
    {
        var queueDiagnostics = new FakeQueueDiagnosticsService();
        const string adminKey = "test-admin-key";
        queueDiagnostics.Queues.Add(new QueueDiagnosticsInfo(
            "process-incoming-message",
            1L,
            [
                new QueueDiagnosticsMessage(
                    "message-1",
                    5,
                    "2CF24DBA5FB0",
                    0,
                    new DateTimeOffset(2026, 5, 28, 10, 0, 0, TimeSpan.Zero),
                    new DateTimeOffset(2026, 5, 28, 17, 0, 0, TimeSpan.Zero)),
            ]));
        await using var factory = new ApiFactory(configureServices: services =>
        {
            services.RemoveAll<IQueueDiagnosticsService>();
            services.AddSingleton<IQueueDiagnosticsService>(queueDiagnostics);
            services.Configure<CeoAgent.ApiService.Infrastructure.Security.AdminApiKeyOptions>(options =>
            {
                options.Key = adminKey;
            });
        });
        using var client = factory.CreateClient();

        using var request = new HttpRequestMessage(HttpMethod.Get, "/v1/admin/queues?maxMessages=5");
        request.Headers.Add("X-Admin-Api-Key", adminKey);

        using var response = await client.SendAsync(request);
        var body = await response.Content.ReadFromJsonAsync<QueuesDiagnosticsResponse>();

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        body.ShouldNotBeNull();
        body.Queues.Single().Name.ShouldBe("process-incoming-message");
        body.Queues.Single().Messages.Single().MessageTextLength.ShouldBe(5);
        body.Queues.Single().Messages.Single().MessageTextSha256Prefix.ShouldBe("2CF24DBA5FB0");
        queueDiagnostics.LastMaxMessages.ShouldBe(5);
    }

    [Test]
    public async Task GetQueueMessages_ReturnsPeekedMessagesForSingleQueue()
    {
        var queueDiagnostics = new FakeQueueDiagnosticsService();
        const string adminKey = "test-admin-key";
        queueDiagnostics.QueueMessages["process-incoming-message"] =
        [
            new QueueDiagnosticsMessage(
                MessageId: "message-1",
                MessageTextLength: 7,
                MessageTextSha256Prefix: "239F59ED55E7",
                DequeueCount: 2,
                InsertedOn: null,
                ExpiresOn: null),
        ];
        await using var factory = new ApiFactory(configureServices: services =>
        {
            services.RemoveAll<IQueueDiagnosticsService>();
            services.AddSingleton<IQueueDiagnosticsService>(queueDiagnostics);
            services.Configure<CeoAgent.ApiService.Infrastructure.Security.AdminApiKeyOptions>(options =>
            {
                options.Key = adminKey;
            });
        });
        using var client = factory.CreateClient();

        using var request = new HttpRequestMessage(HttpMethod.Get, "/v1/admin/queues/process-incoming-message/messages?maxMessages=3");
        request.Headers.Add("X-Admin-Api-Key", adminKey);

        using var response = await client.SendAsync(request);
        var body = await response.Content.ReadFromJsonAsync<QueueMessagesResponse>();

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        body.ShouldNotBeNull();
        body.QueueName.ShouldBe("process-incoming-message");
        body.Messages.Single().MessageTextLength.ShouldBe(7);
        body.Messages.Single().MessageTextSha256Prefix.ShouldBe("239F59ED55E7");
        queueDiagnostics.LastQueueName.ShouldBe("process-incoming-message");
        queueDiagnostics.LastMaxMessages.ShouldBe(3);
    }

    [Test]
    public void QueueDiagnosticsInfo_UsesLongApproximateMessageCount()
    {
        typeof(QueueDiagnosticsInfo)
            .GetProperty(nameof(QueueDiagnosticsInfo.ApproximateMessagesCount))!
            .PropertyType
            .ShouldBe(typeof(long?));
    }

    private sealed class FakeQueueDiagnosticsService : IQueueDiagnosticsService
    {
        public List<QueueMessageSendRequest> SentMessages { get; } = [];

        public List<QueueDiagnosticsInfo> Queues { get; } = [];

        public Dictionary<string, IReadOnlyList<QueueDiagnosticsMessage>> QueueMessages { get; } = [];

        public int? LastMaxMessages { get; private set; }

        public string? LastQueueName { get; private set; }

        public Task<QueueMessageEnqueuedResponse> SendMessageAsync(
            QueueMessageSendRequest request,
            CancellationToken cancellationToken)
        {
            SentMessages.Add(request);
            return Task.FromResult(new QueueMessageEnqueuedResponse(request.QueueName, "message-1"));
        }

        public Task<QueuesDiagnosticsResponse> GetQueuesAsync(
            int maxMessages,
            int maxQueues,
            string? queueNamePrefix,
            string? continuationToken,
            CancellationToken cancellationToken)
        {
            LastMaxMessages = maxMessages;
            return Task.FromResult(new QueuesDiagnosticsResponse(Queues));
        }

        public Task<QueueMessagesResponse> PeekMessagesAsync(
            string queueName,
            int maxMessages,
            CancellationToken cancellationToken)
        {
            LastQueueName = queueName;
            LastMaxMessages = maxMessages;
            QueueMessages.TryGetValue(queueName, out var messages);
            return Task.FromResult(new QueueMessagesResponse(queueName, messages ?? []));
        }
    }
}
