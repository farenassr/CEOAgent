using System.Text.Json;
using Azure.Storage.Queues;
using CeoAgent.Application.Abstractions.Jobs;
using CeoAgent.Shared.Jobs;

namespace CeoAgent.ApiService.Modules.WhatsApp;

public sealed class AzureIncomingMessageJobEnqueuer(QueueServiceClient queueServiceClient) : IIncomingMessageJobEnqueuer
{
    public const string QueueName = IncomingMessageQueueNames.ProcessIncomingMessage;

    public async Task EnqueueAsync(ProcessIncomingMessageJob job, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(job);

        var queue = queueServiceClient.GetQueueClient(QueueName);
        await queue.SendMessageAsync(JsonSerializer.Serialize(job), cancellationToken);
    }
}
