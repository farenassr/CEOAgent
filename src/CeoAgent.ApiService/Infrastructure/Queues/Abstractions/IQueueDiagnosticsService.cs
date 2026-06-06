using CeoAgent.ApiService.Infrastructure.Queues.Contracts;

namespace CeoAgent.ApiService.Infrastructure.Queues.Abstractions;

public interface IQueueDiagnosticsService
{
    Task<QueueMessageEnqueuedResponse> SendMessageAsync(
        QueueMessageSendRequest request,
        CancellationToken cancellationToken);

    Task<QueuesDiagnosticsResponse> GetQueuesAsync(
        int maxMessages,
        int maxQueues,
        string? queueNamePrefix,
        string? continuationToken,
        CancellationToken cancellationToken);

    Task<QueueMessagesResponse> PeekMessagesAsync(
        string queueName,
        int maxMessages,
        CancellationToken cancellationToken);
}
