using CeoAgent.Shared.Enums;

namespace CeoAgent.Worker.Jobs;

public sealed record AudioBlobStoreRequest(
    string Path,
    Stream Content,
    string ContentType,
    long SizeBytes,
    AudioBlobDirection Direction);

public sealed record AudioBlobStoreResult(
    Uri BlobUri,
    long SizeBytes);
