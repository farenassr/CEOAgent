using System.Text.Json.Serialization;

namespace CeoAgent.Infrastructure.Entities.JsonDocuments;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "$provider")]
[JsonDerivedType(typeof(GoogleCalendarCredentialMetadata), "google_calendar")]
[JsonDerivedType(typeof(WhatsAppCloudCredentialMetadata), "whatsapp_cloud")]
[JsonDerivedType(typeof(GenericOAuthCredentialMetadata), "generic_oauth")]
public abstract class CredentialMetadata;

public sealed class GoogleCalendarCredentialMetadata : CredentialMetadata
{
    public required string CalendarId { get; set; }

    public required string Scope { get; set; }

    public DateTimeOffset? ExpiresAt { get; set; }
}

public sealed class WhatsAppCloudCredentialMetadata : CredentialMetadata
{
    public required string AppId { get; set; }

    public required string TokenVersion { get; set; }
}

public sealed class GenericOAuthCredentialMetadata : CredentialMetadata
{
    public required string Scope { get; set; }

    public DateTimeOffset? ExpiresAt { get; set; }
}
