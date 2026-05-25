using System.Text.Json.Serialization;

namespace CeoAgent.Infrastructure.Entities.JsonDocuments;

public sealed class CredentialMetadata
{
    /// <summary>
    /// External provider name stored in the credential metadata payload.
    /// </summary>
    public required string Provider { get; set; }

    /// <summary>
    /// Google Calendar-specific credential metadata.
    /// </summary>
    [JsonPropertyName("google_calendar")]
    public GoogleCalendarCredentialMetadata? GoogleCalendar { get; set; }

    /// <summary>
    /// WhatsApp Cloud-specific credential metadata.
    /// </summary>
    [JsonPropertyName("whatsapp_cloud")]
    public WhatsAppCloudCredentialMetadata? WhatsAppCloud { get; set; }

    public static CredentialMetadata ForGoogleCalendar(GoogleCalendarCredentialMetadata metadata)
    {
        ArgumentNullException.ThrowIfNull(metadata);

        return new CredentialMetadata
        {
            Provider = "google_calendar",
            GoogleCalendar = metadata,
        };
    }

    public static CredentialMetadata ForWhatsAppCloud(WhatsAppCloudCredentialMetadata metadata)
    {
        ArgumentNullException.ThrowIfNull(metadata);

        return new CredentialMetadata
        {
            Provider = "whatsapp_cloud",
            WhatsAppCloud = metadata,
        };
    }

}

public sealed class GoogleCalendarCredentialMetadata
{
    /// <summary>
    /// Google Calendar identifier used by calendar tools.
    /// </summary>
    public required string CalendarId { get; set; }

    /// <summary>
    /// OAuth scope granted to the stored credential.
    /// </summary>
    public required string Scope { get; set; }

    /// <summary>
    /// UTC expiration timestamp for the credential when available.
    /// </summary>
    public DateTimeOffset? ExpiresAt { get; set; }
}

public sealed class WhatsAppCloudCredentialMetadata
{
    /// <summary>
    /// WhatsApp application identifier associated with the credential.
    /// </summary>
    public required string AppId { get; set; }

    /// <summary>
    /// WhatsApp token or API version label associated with the credential.
    /// </summary>
    public required string TokenVersion { get; set; }
}
