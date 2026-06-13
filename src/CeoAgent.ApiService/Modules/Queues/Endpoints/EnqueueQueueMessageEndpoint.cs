using CeoAgent.ApiService.Infrastructure.OpenApi;
using CeoAgent.ApiService.Infrastructure.Queues.Abstractions;
using CeoAgent.ApiService.Infrastructure.Queues;
using CeoAgent.ApiService.Infrastructure.Queues.Contracts;
using CeoAgent.ApiService.Modules.Queues.Contracts;
using FastEndpoints;
using FluentValidation;
using Microsoft.Extensions.Options;

namespace CeoAgent.ApiService.Modules.Queues.Endpoints;

public sealed class EnqueueQueueMessageEndpoint(
    IQueueDiagnosticsService queueDiagnosticsService,
    IOptions<QueueDiagnosticsOptions> options) : Endpoint<SendQueueMessageRequest, QueueMessageEnqueuedResponse>
{
    public override void Configure()
    {
        Post("/v1/admin/queues/{queueName}/messages");
        Description(builder => builder
            .WithTags(OpenApiConstants.Tags.Queues)
            .WithSummary("Replay Queue Message")
            .WithDescription("Sends a diagnostic message to a named queue when queue writes are enabled. Use it to replay or seed queue processing flows from admin tooling."));
        Summary(summary =>
        {
            summary.Summary = "Replay Queue Message";
            summary.Description = "Sends a diagnostic message to a named queue when queue writes are enabled. Use it to replay or seed queue processing flows from admin tooling.";
        });
    }

    public override async Task HandleAsync(SendQueueMessageRequest request, CancellationToken cancellationToken)
    {
        if (!options.Value.EnableWrites)
        {
            await Send.ForbiddenAsync(cancellationToken);
            return;
        }

        var queueName = Route<string>("queueName") ?? string.Empty;
        var response = await queueDiagnosticsService.SendMessageAsync(
            new QueueMessageSendRequest(queueName, request.MessageText),
            cancellationToken);

        await Send.OkAsync(response, cancellationToken);
    }
}

public sealed class SendQueueMessageValidator : Validator<SendQueueMessageRequest>
{
    public SendQueueMessageValidator()
    {
        RuleFor(request => request.MessageText)
            .NotEmpty()
            .MaximumLength(64 * 1024);
    }
}
