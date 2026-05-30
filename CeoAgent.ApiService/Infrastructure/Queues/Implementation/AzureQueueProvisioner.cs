using Azure.Storage.Queues;
using CeoAgent.Integrations.Jobs;
using Microsoft.Extensions.Options;

namespace CeoAgent.ApiService.Infrastructure.Queues.Implementation;

/// <summary>
/// Ensures static Azure Storage queues exist before request handlers enqueue messages.
/// </summary>
public sealed class AzureQueueProvisioner(
    QueueServiceClient queueServiceClient,
    IOptions<QueueDiagnosticsOptions> options) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var queueNames = options.Value.AllowedQueueNames
            .Append(IncomingMessageQueueNames.ProcessIncomingMessage)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.OrdinalIgnoreCase);

        foreach (var queueName in queueNames)
        {
            await queueServiceClient
                .GetQueueClient(queueName)
                .CreateIfNotExistsAsync(cancellationToken: cancellationToken);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
