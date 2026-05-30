namespace CeoAgent.Integrations.Messaging;

public sealed record DownloadedMedia(
    Stream Content,
    string ContentType,
    string OriginalExtension,
    long? SizeBytes);
