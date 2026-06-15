namespace CeoAgent.Shared.Storage;

public sealed record BlobStorageUploadResult(
    BlobStorageReference Reference,
    string BlobUri);
