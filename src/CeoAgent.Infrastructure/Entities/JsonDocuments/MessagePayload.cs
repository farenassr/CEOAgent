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

}
