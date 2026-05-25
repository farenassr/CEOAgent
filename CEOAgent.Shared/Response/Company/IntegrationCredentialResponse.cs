using System.Text.Json;

namespace CeoAgent.Shared.Response.Company;

public sealed class IntegrationCredentialResponse
{
    /// <summary>
    /// Unique credential reference identifier. Example: 018f4f70-8b5f-7b4c-9d1a-0f6c1d7a2b42.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Company that owns this credential reference. Example: 018f4f70-8b5f-7b4c-9d1a-0f6c1d7a2b30.
    /// </summary>
    public Guid CompanyId { get; set; }

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
    /// Optional provider-specific metadata as JSON.
    /// </summary>
    public JsonElement? Metadata { get; set; }
}
