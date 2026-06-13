using CeoAgent.ApiService.Infrastructure.OpenApi;
using CeoAgent.ApiService.Infrastructure.Queues.Abstractions;
using CeoAgent.ApiService.Infrastructure.Queues.Contracts;
using FastEndpoints;

namespace CeoAgent.ApiService.Modules.Queues.Endpoints;

public sealed class GetQueueMessagesEndpoint(
    IQueueDiagnosticsService queueDiagnosticsService) : EndpointWithoutRequest<QueueMessagesResponse>
{
    public override void Configure()
    {
        Get("/v1/admin/queues/{queueName}/messages");
        Description(builder => builder
            .WithTags(OpenApiConstants.Tags.Queues)
            .WithSummary("Peek Queue Messages")
            .WithDescription("Reads visible messages from a named queue without dequeuing them. Use it for diagnostics when investigating background job or integration flow issues."));
        Summary(summary =>
        {
            summary.Summary = "Peek Queue Messages";
            summary.Description = "Reads visible messages from a named queue without dequeuing them. Use it for diagnostics when investigating background job or integration flow issues.";
        });
    }

    public override async Task HandleAsync(CancellationToken cancellationToken)
    {
        var queueName = Route<string>("queueName") ?? string.Empty;
        var maxMessages = Query<int>("maxMessages", isRequired: false);
        var response = await queueDiagnosticsService.PeekMessagesAsync(
            queueName,
            maxMessages,
            cancellationToken);

        await Send.OkAsync(response, cancellationToken);
    }
}
