namespace CeoAgent.Shared.Response.Queues;

public sealed record QueueMessagesResponse(
    string QueueName,
    IReadOnlyList<QueueDiagnosticsMessage> Messages);
