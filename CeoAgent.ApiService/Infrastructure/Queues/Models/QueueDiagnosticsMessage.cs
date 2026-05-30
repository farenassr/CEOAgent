namespace CeoAgent.ApiService.Infrastructure.Queues.Contracts;

public sealed record QueueDiagnosticsMessage(
    string MessageId,
    int MessageTextLength,
    string MessageTextSha256Prefix,
    long DequeueCount,
    DateTimeOffset? InsertedOn,
    DateTimeOffset? ExpiresOn);
