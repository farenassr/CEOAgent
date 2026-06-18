namespace CeoAgent.Shared.Response.Queues;

public sealed record QueueDiagnosticsMessage(
    string MessageId,
    int MessageTextLength,
    string MessageTextSha256Prefix,
    long DequeueCount,
    DateTimeOffset? InsertedOn,
    DateTimeOffset? ExpiresOn);
