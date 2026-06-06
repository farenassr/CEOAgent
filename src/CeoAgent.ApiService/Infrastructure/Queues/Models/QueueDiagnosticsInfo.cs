namespace CeoAgent.ApiService.Infrastructure.Queues.Contracts;

public sealed record QueueDiagnosticsInfo(
    string Name,
    long? ApproximateMessagesCount,
    IReadOnlyList<QueueDiagnosticsMessage> Messages);
