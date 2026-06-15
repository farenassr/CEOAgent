namespace CeoAgent.Shared.Storage;

public static class BlobStorageContainerNames
{
    public const string Private = "private";
    public const string Public = "public";

    public static bool IsAllowed(string containerName)
    {
        return string.Equals(containerName, Private, StringComparison.Ordinal)
            || string.Equals(containerName, Public, StringComparison.Ordinal);
    }
}
