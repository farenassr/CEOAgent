using CeoAgent.Shared.Enums;

namespace CeoAgent.Infrastructure.Entities.JsonDocuments;

public sealed class MessagePayload
{
    /// <summary>
    /// Provider-specific message type, such as text or audio.
    /// </summary>
    public string? ProviderType { get; set; }

    /// <summary>
    /// Provider-side message identifier copied into the payload for metadata traceability.
    /// </summary>
    public string? ProviderMessageId { get; set; }

    /// <summary>
    /// Audio metadata when the message carries or generated audio.
    /// </summary>
    public AudioPayload? Audio { get; set; }

    public static MessagePayload ForAudio(
        string providerType,
        AudioPayload payload,
        string? providerMessageId = null)
    {
        ArgumentNullException.ThrowIfNull(payload);

        return new MessagePayload
        {
            ProviderType = providerType,
            ProviderMessageId = providerMessageId,
            Audio = payload,
        };
    }
}

public abstract class BlobPayload
{
    /// <summary>
    /// Blob storage URI where the payload content is stored.
    /// </summary>
    public required string BlobUri { get; set; }

    /// <summary>
    /// MIME content type for the stored payload.
    /// </summary>
    public required string ContentType { get; set; }

    /// <summary>
    /// Payload size in bytes.
    /// </summary>
    public long SizeBytes { get; set; }
}

public sealed class AudioPayload : BlobPayload
{
    /// <summary>
    /// Detected or configured language for the audio content.
    /// </summary>
    public string? Language { get; set; }

    /// <summary>
    /// Audio duration in milliseconds when known.
    /// </summary>
    public int? DurationMs { get; set; }

    /// <summary>
    /// Speech-to-text processing status for inbound audio.
    /// </summary>
    public SpeechProcessingStatus? SttStatus { get; set; }

    /// <summary>
    /// Text-to-speech processing status for outbound audio.
    /// </summary>
    public SpeechProcessingStatus? TtsStatus { get; set; }
}
