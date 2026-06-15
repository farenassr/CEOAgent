namespace CeoAgent.Shared.Storage;

public sealed record BlobStorageDownloadResult(
    byte[] Content,
    string ContentType,
    string FileName);
