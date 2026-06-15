namespace CeoAgent.Shared.Storage;

public sealed record StoredFile(
    byte[] Content,
    string ContentType,
    string FileName);
