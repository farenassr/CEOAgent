namespace CeoAgent.Shared.Storage;

public sealed record BlobStorageReference(string ContainerName, string BlobName)
{
    public static BlobStorageReference Create(string containerName, string blobName)
    {
        if (!BlobStorageContainerNames.IsAllowed(containerName))
        {
            throw new ArgumentException("Blob container must be private or public.", nameof(containerName));
        }

        if (string.IsNullOrWhiteSpace(blobName))
        {
            throw new ArgumentException("Blob name is required.", nameof(blobName));
        }

        var normalizedBlobName = blobName.Trim();
        if (normalizedBlobName.StartsWith('/')
            || normalizedBlobName.Contains('\\'))
        {
            throw new ArgumentException("Blob name must be a relative slash-delimited path.", nameof(blobName));
        }

        return new BlobStorageReference(containerName, normalizedBlobName);
    }
}
