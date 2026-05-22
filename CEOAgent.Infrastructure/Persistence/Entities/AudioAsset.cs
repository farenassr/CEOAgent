using CEOAgent.Shared.Enums;

namespace CEOAgent.Infrastructure.Persistence.Entities;

public sealed class AudioAsset : AuditableCompanyOwnedEntity
{
    /// <summary>
    /// Unique audio asset identifier. Example: 018f4f70-8b5f-7b4c-9d1a-0f6c1d7a2b41.
    /// </summary>
    public Guid Id { get; set; } = Guid.CreateVersion7();

    /// <summary>
    /// Conversation associated with the audio asset when available. Example: 018f4f70-8b5f-7b4c-9d1a-0f6c1d7a2b34.
    /// </summary>
    public Guid? ConversationId { get; set; }

    /// <summary>
    /// Message associated with the audio asset when available. Example: 018f4f70-8b5f-7b4c-9d1a-0f6c1d7a2b36.
    /// </summary>
    public Guid? MessageId { get; set; }

    /// <summary>
    /// Direction of the audio asset. Example: Inbound.
    /// </summary>
    public AudioAssetDirection Direction { get; set; }

    /// <summary>
    /// Blob URI where the audio file is stored. Example: https://storage.example/audio/inbound/voice.ogg.
    /// </summary>
    public required string BlobUri { get; set; }

    /// <summary>
    /// MIME content type for the audio file. Example: audio/ogg.
    /// </summary>
    public required string ContentType { get; set; }

    /// <summary>
    /// Audio file size in bytes. Example: 184320.
    /// </summary>
    public long SizeBytes { get; set; }

    /// <summary>
    /// Transcribed text for inbound audio when available. Example: Necesito una mesa para cuatro.
    /// </summary>
    public string? Transcript { get; set; }
}
