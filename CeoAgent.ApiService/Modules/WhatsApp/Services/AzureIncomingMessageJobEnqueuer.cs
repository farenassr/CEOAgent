using System.Text.Json;
using Azure.Storage.Queues;
using CeoAgent.Integrations.Jobs;

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
