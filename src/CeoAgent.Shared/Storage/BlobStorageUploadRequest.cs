namespace CeoAgent.Shared.Storage;

public sealed record BlobStorageUploadRequest(
    BlobStorageReference Reference,
    Stream Content,
    string ContentType,
    IReadOnlyDictionary<string, string> Tags);
