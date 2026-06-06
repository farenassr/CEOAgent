namespace CeoAgent.ApiService.Infrastructure.Queues.Contracts;

public sealed record QueueMessagesResponse(
    string QueueName,
    IReadOnlyList<QueueDiagnosticsMessage> Messages);
