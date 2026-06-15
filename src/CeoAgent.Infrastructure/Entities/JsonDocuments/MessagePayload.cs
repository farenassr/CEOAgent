namespace CeoAgent.Infrastructure.Entities.JsonDocuments;

public sealed class MessagePayload
{
    /// <summary>
    /// Provider-specific message type.
    /// </summary>
    public string? ProviderType { get; set; }

    /// <summary>
    /// Provider-side message identifier copied into the payload for metadata traceability.
    /// </summary>
    public string? ProviderMessageId { get; set; }

    /// <summary>
    /// Provider-side media identifier for image/audio messages.
    /// </summary>
    public string? ProviderMediaId { get; set; }

    /// <summary>
    /// Media MIME type when known.
    /// </summary>
    public string? MimeType { get; set; }

    /// <summary>
    /// Provider media hash when known.
    /// </summary>
    public string? Sha256 { get; set; }

    /// <summary>
    /// Private blob container used for backend-owned media references.
    /// </summary>
    public string? BlobContainer { get; set; }

    /// <summary>
    /// Private blob name used for backend-owned media references.
    /// </summary>
    public string? BlobName { get; set; }
}
