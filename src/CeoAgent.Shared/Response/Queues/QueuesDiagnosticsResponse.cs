namespace CeoAgent.Shared.Response.Queues;

public sealed record QueuesDiagnosticsResponse(
    IReadOnlyList<QueueDiagnosticsInfo> Queues,
    string? ContinuationToken = null);
