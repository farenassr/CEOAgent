using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using CeoAgent.Application.Abstractions.Storage;
using CeoAgent.Shared.Storage;

namespace CeoAgent.Infrastructure.Implementation.Messaging.Storage;

public sealed class AzureBlobStorageService(BlobServiceClient blobServiceClient) : IBlobStorageService
{
    public async Task<BlobStorageUploadResult> UploadAsync(BlobStorageUploadRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        BlobStorageTags.Validate(request.Tags);

        if (string.IsNullOrWhiteSpace(request.ContentType))
        {
            throw new ArgumentException("Blob content type is required.", nameof(request));
        }

        var container = GetBlobContainerClient(request.Reference);
        var blob = container.GetBlobClient(request.Reference.BlobName);
        await container.CreateIfNotExistsAsync(cancellationToken: cancellationToken);
        await blob.UploadAsync(
            request.Content,
            new BlobUploadOptions
            {
                HttpHeaders = new BlobHttpHeaders
                {
                    ContentType = request.ContentType,
                },
                Tags = new Dictionary<string, string>(request.Tags, StringComparer.Ordinal),
            },
            cancellationToken);
        return new BlobStorageUploadResult(request.Reference, blob.Uri.ToString());
    }

    public async Task<BlobStorageDownloadResult> DownloadAsync(
        BlobStorageReference reference,
        CancellationToken cancellationToken)
    {
        var blob = GetBlobClient(reference);
        var download = await blob.DownloadContentAsync(cancellationToken);
        var contentType = string.IsNullOrWhiteSpace(download.Value.Details.ContentType)
            ? "application/octet-stream"
            : download.Value.Details.ContentType;
        var fileName = Path.GetFileName(reference.BlobName);
        if (string.IsNullOrWhiteSpace(fileName))
        {
            fileName = "blob";
        }

        return new BlobStorageDownloadResult(download.Value.Content.ToArray(), contentType, fileName);
    }

    public async Task<bool> ExistsAsync(BlobStorageReference reference, CancellationToken cancellationToken)
    {
        var response = await GetBlobClient(reference).ExistsAsync(cancellationToken);
        return response.Value;
    }

    public async Task<bool> DeleteIfExistsAsync(BlobStorageReference reference, CancellationToken cancellationToken)
    {
        var response = await GetBlobClient(reference).DeleteIfExistsAsync(cancellationToken: cancellationToken);
        return response.Value;
    }

    public async Task SetTagsAsync(
        BlobStorageReference reference,
        IReadOnlyDictionary<string, string> tags,
        CancellationToken cancellationToken)
    {
        BlobStorageTags.Validate(tags);
        await GetBlobClient(reference).SetTagsAsync(
            new Dictionary<string, string>(tags, StringComparer.Ordinal),
            cancellationToken: cancellationToken);
    }

    public async Task<IReadOnlyDictionary<string, string>> GetTagsAsync(
        BlobStorageReference reference,
        CancellationToken cancellationToken)
    {
        var response = await GetBlobClient(reference).GetTagsAsync(cancellationToken: cancellationToken);
        return new Dictionary<string, string>(response.Value.Tags, StringComparer.Ordinal);
    }

    private BlobClient GetBlobClient(BlobStorageReference reference)
    {
        return GetBlobContainerClient(reference).GetBlobClient(reference.BlobName);
    }

    private BlobContainerClient GetBlobContainerClient(BlobStorageReference reference)
    {
        ArgumentNullException.ThrowIfNull(reference);
        return blobServiceClient.GetBlobContainerClient(reference.ContainerName);
    }
}
