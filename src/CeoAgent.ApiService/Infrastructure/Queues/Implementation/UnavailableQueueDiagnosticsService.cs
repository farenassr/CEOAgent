using CeoAgent.ApiService.Infrastructure.Queues.Abstractions;
using CeoAgent.ApiService.Infrastructure.Queues.Contracts;
using CeoAgent.Application.Errors;
using CeoAgent.Shared.Response.Queues;

namespace CeoAgent.ApiService.Infrastructure.Queues.Implementation;

/// <summary>
/// Fails queue diagnostics operations when Azure Storage Queues are not configured.
/// </summary>
public sealed class UnavailableQueueDiagnosticsService : IQueueDiagnosticsService
{
    /// <summary>
    /// Throws an integration exception because queue diagnostics cannot enqueue without a queue client.
    /// </summary>
    public Task<QueueMessageEnqueuedResponse> SendMessageAsync(
        QueueMessageSendRequest request,
        CancellationToken cancellationToken)
    {
        throw CreateException();
    }

    /// <summary>
    /// Throws an integration exception because queue diagnostics cannot list queues without a queue client.
    /// </summary>
    public Task<QueuesDiagnosticsResponse> GetQueuesAsync(
        int maxMessages,
        int maxQueues,
        string? queueNamePrefix,
        string? continuationToken,
        CancellationToken cancellationToken)
    {
        throw CreateException();
    }

    /// <summary>
    /// Throws an integration exception because queue diagnostics cannot peek messages without a queue client.
    /// </summary>
    public Task<QueueMessagesResponse> PeekMessagesAsync(
        string queueName,
        int maxMessages,
        CancellationToken cancellationToken)
    {
        throw CreateException();
    }

    private static IntegrationException CreateException()
    {
        return new IntegrationException(
            "azure_storage_queues",
            "Queue diagnostics require a configured Azure QueueServiceClient.");
    }
}
