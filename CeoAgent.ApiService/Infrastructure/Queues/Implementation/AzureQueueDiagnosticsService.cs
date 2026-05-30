using Azure.Storage.Queues;
using Azure.Storage.Queues.Models;
using CeoAgent.ApiService.Infrastructure.Queues;
using CeoAgent.ApiService.Infrastructure.Queues.Abstractions;
using CeoAgent.ApiService.Infrastructure.Queues.Contracts;
using CeoAgent.Application.Errors;
using Microsoft.Extensions.Options;
using System.Security.Cryptography;
using System.Text;

namespace CeoAgent.ApiService.Infrastructure.Queues.Implementation;

/// <summary>
/// Provides bounded diagnostic read and write operations over configured Azure Storage Queues.
/// </summary>
public sealed class AzureQueueDiagnosticsService(
    QueueServiceClient queueServiceClient,
    IOptions<QueueDiagnosticsOptions> options) : IQueueDiagnosticsService
{
    private const int AzureQueuePeekLimit = 32;
    private const int MaxQueueDiagnosticsConcurrency = 8;

    /// <summary>
    /// Sends a diagnostic message only to queues explicitly allowed by configuration.
    /// </summary>
    public async Task<QueueMessageEnqueuedResponse> SendMessageAsync(
        QueueMessageSendRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!IsAllowed(request.QueueName))
        {
            throw new BusinessRuleException("queue_not_allowed", $"Queue '{request.QueueName}' is not allowed for diagnostics.");
        }

        var queueClient = queueServiceClient.GetQueueClient(request.QueueName);
        var receipt = await queueClient.SendMessageAsync(request.MessageText, cancellationToken);

        return new QueueMessageEnqueuedResponse(request.QueueName, receipt.Value.MessageId);
    }

    /// <summary>
    /// Lists allowed queues with approximate counts and a bounded peek of visible messages.
    /// </summary>
    public async Task<QueuesDiagnosticsResponse> GetQueuesAsync(
        int maxMessages,
        int maxQueues,
        string? queueNamePrefix,
        string? continuationToken,
        CancellationToken cancellationToken)
    {
        var boundedMaxMessages = BoundMaxMessages(maxMessages);
        var boundedMaxQueues = BoundMaxQueues(maxQueues);
        var offset = ParseContinuationToken(continuationToken);
        var allAllowedNames = AllowedQueueNames(queueNamePrefix);
        var allowedNames = allAllowedNames
            .Skip(offset)
            .Take(boundedMaxQueues)
            .ToArray();
        using var concurrency = new SemaphoreSlim(MaxQueueDiagnosticsConcurrency);

        var inspectedQueues = await Task.WhenAll(
            allowedNames.Select(queueName => TryReadQueueAsync(
                queueName,
                boundedMaxMessages,
                concurrency,
                cancellationToken)));

        var nextOffset = offset + allowedNames.Length;
        var nextToken = nextOffset < allAllowedNames.Length
            ? nextOffset.ToString(System.Globalization.CultureInfo.InvariantCulture)
            : null;

        return new QueuesDiagnosticsResponse(
            inspectedQueues.Where(queue => queue is not null).Select(queue => queue!).ToArray(),
            nextToken);
    }

    /// <summary>
    /// Peeks visible messages from an allowed queue without exposing raw message bodies.
    /// </summary>
    public async Task<QueueMessagesResponse> PeekMessagesAsync(
        string queueName,
        int maxMessages,
        CancellationToken cancellationToken)
    {
        var queueClient = queueServiceClient.GetQueueClient(queueName);
        if (!IsAllowed(queueName))
        {
            return new QueueMessagesResponse(queueName, []);
        }

        var exists = await queueClient.ExistsAsync(cancellationToken);
        if (!exists.Value)
        {
            return new QueueMessagesResponse(queueName, []);
        }

        var messages = await PeekMessagesCoreAsync(
            queueClient,
            BoundMaxMessages(maxMessages),
            cancellationToken);

        return new QueueMessagesResponse(queueName, messages);
    }

    private static async Task<IReadOnlyList<QueueDiagnosticsMessage>> PeekMessagesCoreAsync(
        QueueClient queueClient,
        int maxMessages,
        CancellationToken cancellationToken)
    {
        var messages = await queueClient.PeekMessagesAsync(maxMessages, cancellationToken);

        return messages.Value
            .Select(ToDiagnosticsMessage)
            .ToArray();
    }

    private static QueueDiagnosticsMessage ToDiagnosticsMessage(PeekedMessage message)
    {
        return new QueueDiagnosticsMessage(
            message.MessageId,
            message.MessageText.Length,
            HashPrefix(message.MessageText),
            message.DequeueCount,
            message.InsertedOn,
            message.ExpiresOn);
    }

    private async Task<QueueDiagnosticsInfo?> TryReadQueueAsync(
        string queueName,
        int maxMessages,
        SemaphoreSlim concurrency,
        CancellationToken cancellationToken)
    {
        await concurrency.WaitAsync(cancellationToken);
        try
        {
            var queueClient = queueServiceClient.GetQueueClient(queueName);
            if (!await queueClient.ExistsAsync(cancellationToken))
            {
                return null;
            }

            var properties = await queueClient.GetPropertiesAsync(cancellationToken);
            var messages = await PeekMessagesCoreAsync(queueClient, maxMessages, cancellationToken);

            return new QueueDiagnosticsInfo(
                queueClient.Name,
                properties.Value.ApproximateMessagesCountLong,
                messages);
        }
        finally
        {
            concurrency.Release();
        }
    }

    private string[] AllowedQueueNames(string? queueNamePrefix)
    {
        return options.Value.AllowedQueueNames
            .Where(name => !string.IsNullOrWhiteSpace(name) && (string.IsNullOrWhiteSpace(queueNamePrefix)
                || name.StartsWith(queueNamePrefix, StringComparison.OrdinalIgnoreCase)))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private bool IsAllowed(string queueName)
    {
        return options.Value.AllowedQueueNames
            .Any(name => string.Equals(name, queueName, StringComparison.OrdinalIgnoreCase));
    }

    private int BoundMaxMessages(int maxMessages)
    {
        return maxMessages <= 0
            ? options.Value.DefaultMaxMessages
            : Math.Min(maxMessages, AzureQueuePeekLimit);
    }

    private int BoundMaxQueues(int maxQueues)
    {
        return maxQueues <= 0
            ? Math.Max(1, options.Value.DefaultMaxQueues)
            : Math.Min(maxQueues, 500);
    }

    private static int ParseContinuationToken(string? continuationToken)
    {
        return int.TryParse(
            continuationToken,
            System.Globalization.NumberStyles.None,
            System.Globalization.CultureInfo.InvariantCulture,
            out var offset)
            && offset > 0
                ? offset
                : 0;
    }

    private static string HashPrefix(string value)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(hash)[..12];
    }
}
