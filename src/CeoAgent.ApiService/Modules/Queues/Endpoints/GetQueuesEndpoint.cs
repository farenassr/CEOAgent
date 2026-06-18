using CeoAgent.ApiService.Infrastructure.OpenApi;
using CeoAgent.ApiService.Infrastructure.Queues.Abstractions;
using CeoAgent.Shared.Response.Queues;
using FastEndpoints;

namespace CeoAgent.ApiService.Modules.Queues.Endpoints;

public sealed class GetQueuesEndpoint(
    IQueueDiagnosticsService queueDiagnosticsService) : EndpointWithoutRequest<QueuesDiagnosticsResponse>
{
    public override void Configure()
    {
        Get("/v1/admin/queues");
        Description(builder => builder
            .WithTags(OpenApiConstants.Tags.Queues)
            .WithSummary("List Queues")
            .WithDescription("Lists configured Azure Queue diagnostics with optional prefix and paging controls. Use it to inspect queue health and message counts in admin tooling."));
        Summary(summary =>
        {
            summary.Summary = "List Queues";
            summary.Description = "Lists configured Azure Queue diagnostics with optional prefix and paging controls. Use it to inspect queue health and message counts in admin tooling.";
        });
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
