namespace CeoAgent.ApiService.Infrastructure.Queues.Contracts;

public sealed record QueuesDiagnosticsResponse(
    IReadOnlyList<QueueDiagnosticsInfo> Queues,
    string? ContinuationToken = null);
