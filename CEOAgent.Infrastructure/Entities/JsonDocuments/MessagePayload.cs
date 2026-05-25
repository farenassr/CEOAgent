using System.Text.Json.Serialization;

namespace CeoAgent.Infrastructure.Entities.JsonDocuments;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "$messageType")]
[JsonDerivedType(typeof(TextPayload), "text")]
[JsonDerivedType(typeof(MediaPayload), "media")]
[JsonDerivedType(typeof(InteractivePayload), "interactive")]
[JsonDerivedType(typeof(LocationPayload), "location")]
public abstract class MessagePayload
{
    public required string ProviderType { get; set; }

    public string? ProviderMessageId { get; set; }
}

public sealed class TextPayload : MessagePayload
{
    public required string Body { get; set; }
}

public sealed class MediaPayload : MessagePayload
{
    public required string MediaUrl { get; set; }

    public required string MimeType { get; set; }

    public long? SizeBytes { get; set; }

    public string? Caption { get; set; }
}

public sealed class InteractivePayload : MessagePayload
{
    public required string InteractionType { get; set; }

    public string? SelectedId { get; set; }

    public string? SelectedTitle { get; set; }
}

public sealed class LocationPayload : MessagePayload
{
    public double Latitude { get; set; }

    public double Longitude { get; set; }
}
