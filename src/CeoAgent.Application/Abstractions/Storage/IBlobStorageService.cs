using CeoAgent.Shared.Storage;

namespace CeoAgent.Application.Abstractions.Storage;

public interface IBlobStorageService
{
    Task<BlobStorageUploadResult> UploadAsync(BlobStorageUploadRequest request, CancellationToken cancellationToken);

    Task<BlobStorageDownloadResult> DownloadAsync(BlobStorageReference reference, CancellationToken cancellationToken);

    Task<bool> ExistsAsync(BlobStorageReference reference, CancellationToken cancellationToken);

    Task<bool> DeleteIfExistsAsync(BlobStorageReference reference, CancellationToken cancellationToken);

    Task SetTagsAsync(
        BlobStorageReference reference,
        IReadOnlyDictionary<string, string> tags,
        CancellationToken cancellationToken);

    Task<IReadOnlyDictionary<string, string>> GetTagsAsync(
        BlobStorageReference reference,
        CancellationToken cancellationToken);
}
