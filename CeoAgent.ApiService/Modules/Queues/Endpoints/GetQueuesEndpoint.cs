using CeoAgent.ApiService.Infrastructure.Queues.Abstractions;
using CeoAgent.ApiService.Infrastructure.Queues.Contracts;
using FastEndpoints;

namespace CeoAgent.ApiService.Modules.Queues.Endpoints;

public sealed class GetQueuesEndpoint(
    IQueueDiagnosticsService queueDiagnosticsService) : EndpointWithoutRequest<QueuesDiagnosticsResponse>
{
    public override void Configure()
    {
        Get("/v1/admin/queues");
    }

    public override async Task HandleAsync(CancellationToken cancellationToken)
    {
        var maxMessages = Query<int>("maxMessages", isRequired: false);
        var maxQueues = Query<int>("maxQueues", isRequired: false);
        var queueNamePrefix = Query<string>("prefix", isRequired: false);
        var continuationToken = Query<string>("continuationToken", isRequired: false);
        var response = await queueDiagnosticsService.GetQueuesAsync(
            maxMessages,
            maxQueues,
            queueNamePrefix,
            continuationToken,
            cancellationToken);

        await Send.OkAsync(response, cancellationToken);
    }
}
