using CeoAgent.ApiService.Infrastructure.Queues.Abstractions;
using CeoAgent.ApiService.Infrastructure.Queues.Contracts;
using CeoAgent.ApiService.Modules.Queues.Contracts;
using FastEndpoints;
using FluentValidation;

namespace CeoAgent.ApiService.Modules.Queues.Endpoints;

public sealed class EnqueueQueueMessageEndpoint(
    IQueueDiagnosticsService queueDiagnosticsService) : Endpoint<SendQueueMessageRequest, QueueMessageEnqueuedResponse>
{
    public override void Configure()
    {
        Post("/v1/admin/queues/{queueName}/messages");
    }

    public override async Task HandleAsync(SendQueueMessageRequest request, CancellationToken cancellationToken)
    {
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
