using CeoAgent.Application.Abstractions.Storage;
using CeoAgent.Shared.Storage;

namespace CeoAgent.Infrastructure.Implementation.Messaging.Storage;

public sealed class UnavailableBlobStorageService : IBlobStorageService
{
    public Task<BlobStorageUploadResult> UploadAsync(BlobStorageUploadRequest request, CancellationToken cancellationToken)
    {
        throw CreateUnavailableException();
    }

    public Task<BlobStorageDownloadResult> DownloadAsync(
        BlobStorageReference reference,
        CancellationToken cancellationToken)
    {
        throw CreateUnavailableException();
    }

    public Task<bool> ExistsAsync(BlobStorageReference reference, CancellationToken cancellationToken)
    {
        throw CreateUnavailableException();
    }

    public Task<bool> DeleteIfExistsAsync(BlobStorageReference reference, CancellationToken cancellationToken)
    {
        throw CreateUnavailableException();
    }

    public Task SetTagsAsync(
        BlobStorageReference reference,
        IReadOnlyDictionary<string, string> tags,
        CancellationToken cancellationToken)
    {
        throw CreateUnavailableException();
    }

    public Task<IReadOnlyDictionary<string, string>> GetTagsAsync(
        BlobStorageReference reference,
        CancellationToken cancellationToken)
    {
        throw CreateUnavailableException();
    }

    private static InvalidOperationException CreateUnavailableException()
    {
        return new InvalidOperationException("Azure Blob Storage is not configured.");
    }
}
