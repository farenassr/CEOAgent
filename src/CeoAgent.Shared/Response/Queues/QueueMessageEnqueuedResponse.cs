namespace CeoAgent.Shared.Response.Queues;

public sealed record QueueMessageEnqueuedResponse(
    string QueueName,
    string MessageId);
