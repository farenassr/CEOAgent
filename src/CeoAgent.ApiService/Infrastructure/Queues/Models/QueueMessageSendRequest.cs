namespace CeoAgent.ApiService.Infrastructure.Queues.Contracts;

public sealed record QueueMessageSendRequest(
    string QueueName,
    string MessageText);
