namespace CeoAgent.Shared.Storage;

public sealed record StoredFileReadOptions(
    string DefaultContentType = "application/octet-stream",
    string DefaultFileName = "blob");
