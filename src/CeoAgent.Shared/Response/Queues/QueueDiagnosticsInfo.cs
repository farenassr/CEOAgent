namespace CeoAgent.Shared.Response.Queues;

public sealed record QueueDiagnosticsInfo(
    string Name,
    long? ApproximateMessagesCount,
    IReadOnlyList<QueueDiagnosticsMessage> Messages);
