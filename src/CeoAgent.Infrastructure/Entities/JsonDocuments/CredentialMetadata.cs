using System.Text.Json;
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
    private static readonly JsonSerializerOptions ServiceAccountSerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    /// <summary>
    /// Google Calendar identifier used by calendar tools.
    /// </summary>
    public string? CalendarId { get; set; }

    /// <summary>
    /// OAuth scope granted to the stored credential.
    /// </summary>
    public string? Scope { get; set; }

    /// <summary>
    /// UTC expiration timestamp for the credential when available.
    /// </summary>
    public DateTimeOffset? ExpiresAt { get; set; }

    [JsonPropertyName("type")]
    public required string Type { get; set; }

    [JsonPropertyName("project_id")]
    public required string ProjectId { get; set; }

    [JsonPropertyName("private_key_id")]
    public required string PrivateKeyId { get; set; }

    [JsonPropertyName("private_key")]
    public required string PrivateKey { get; set; }

    [JsonPropertyName("client_email")]
    public required string ClientEmail { get; set; }

    [JsonPropertyName("client_id")]
    public required string ClientId { get; set; }

    [JsonPropertyName("auth_uri")]
    public required string AuthUri { get; set; }

    [JsonPropertyName("token_uri")]
    public required string TokenUri { get; set; }

    [JsonPropertyName("auth_provider_x509_cert_url")]
    public required string AuthProviderX509CertUrl { get; set; }

    [JsonPropertyName("client_x509_cert_url")]
    public required string ClientX509CertUrl { get; set; }

    [JsonPropertyName("universe_domain")]
    public required string UniverseDomain { get; set; }

    public bool HasServiceAccountCredentials()
    {
        return !string.IsNullOrWhiteSpace(Type)
            && !string.IsNullOrWhiteSpace(ProjectId)
            && !string.IsNullOrWhiteSpace(PrivateKeyId)
            && !string.IsNullOrWhiteSpace(PrivateKey)
            && !string.IsNullOrWhiteSpace(ClientEmail)
            && !string.IsNullOrWhiteSpace(ClientId)
            && !string.IsNullOrWhiteSpace(AuthUri)
            && !string.IsNullOrWhiteSpace(TokenUri)
            && !string.IsNullOrWhiteSpace(AuthProviderX509CertUrl)
            && !string.IsNullOrWhiteSpace(ClientX509CertUrl)
            && !string.IsNullOrWhiteSpace(UniverseDomain);
    }

    public string ToServiceAccountJson()
    {
        return JsonSerializer.Serialize(this, ServiceAccountSerializerOptions);
    }
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
