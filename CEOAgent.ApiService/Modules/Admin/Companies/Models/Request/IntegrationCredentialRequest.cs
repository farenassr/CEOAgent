namespace CEOAgent.ApiService.Modules.Admin.Companies.Models.Request;

public sealed class IntegrationCredentialRequest
{
    /// <summary>
    /// Integration provider key. Example: google_calendar.
    /// </summary>
    public string Provider { get; set; } = string.Empty;

    /// <summary>
    /// Purpose for the credential reference. Example: whatsapp_cloud.
    /// </summary>
    public string Purpose { get; set; } = string.Empty;

    /// <summary>
    /// External secret or credential reference. Example: kv://google-calendar/contoso.
    /// </summary>
    public string Reference { get; set; } = string.Empty;

    /// <summary>
    /// Optional provider-specific metadata as JSON. Example: {"calendarId":"primary"}.
    /// </summary>
    public string? MetadataJson { get; set; }
}
