using System.ComponentModel;
using System.Text.Json;

namespace CeoAgent.Shared.Request.Company;

public sealed class IntegrationCredentialRequest
{
    /// <summary>
    /// Integration provider key.
    /// </summary>
    [Description("Integration provider key.")]
    [DefaultValue("google_calendar")]
    public string Provider { get; set; } = string.Empty;

    /// <summary>
    /// Purpose for the credential reference.
    /// </summary>
    [Description("Purpose for the credential reference.")]
    [DefaultValue("whatsapp_cloud")]
    public string Purpose { get; set; } = string.Empty;

    /// <summary>
    /// External secret or credential reference.
    /// </summary>
    [Description("External secret or credential reference.")]
    [DefaultValue("kv://google-calendar/contoso")]
    public string Reference { get; set; } = string.Empty;

    /// <summary>
    /// Optional provider-specific metadata as JSON.
    /// </summary>
    [Description("Optional provider-specific metadata as JSON.")]
    public JsonElement? Metadata { get; set; }
}
