using CeoAgent.Application.Abstractions.Storage;
using CeoAgent.Shared.Storage;

namespace CeoAgent.Infrastructure.Implementation.Messaging.Storage;

public sealed class BlobStoredFileReader(IBlobStorageService blobStorage) : IStoredFileReader
{
    public async Task<StoredFile> ReadAsync(
        BlobStorageReference reference,
        StoredFileReadOptions options,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(reference);
        ArgumentNullException.ThrowIfNull(options);

        var download = await blobStorage.DownloadAsync(reference, cancellationToken);
        var contentType = string.IsNullOrWhiteSpace(download.ContentType)
            ? options.DefaultContentType
            : download.ContentType;
        var fileName = string.IsNullOrWhiteSpace(download.FileName)
            ? options.DefaultFileName
            : download.FileName;

        return new StoredFile(download.Content, contentType, fileName);
    }
}
