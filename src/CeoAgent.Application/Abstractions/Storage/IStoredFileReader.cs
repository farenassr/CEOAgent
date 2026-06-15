using CeoAgent.Shared.Storage;

namespace CeoAgent.Application.Abstractions.Storage;

public interface IStoredFileReader
{
    Task<StoredFile> ReadAsync(
        BlobStorageReference reference,
        StoredFileReadOptions options,
        CancellationToken cancellationToken);
}
