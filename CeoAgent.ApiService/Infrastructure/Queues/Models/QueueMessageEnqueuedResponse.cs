namespace CeoAgent.ApiService.Infrastructure.Queues.Contracts;

public sealed record QueueMessageEnqueuedResponse(
    string QueueName,
    string MessageId);
