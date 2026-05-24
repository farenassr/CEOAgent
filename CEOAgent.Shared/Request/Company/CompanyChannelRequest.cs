using System.ComponentModel;
using System.Text.Json;

namespace CEOAgent.Shared.Request.Company;

public sealed class CompanyChannelRequest
{
    /// <summary>
    /// Channel provider key.
    /// </summary>
    [Description("Channel provider key.")]
    [DefaultValue("whatsapp_cloud")]
    public string Provider { get; set; } = string.Empty;

    /// <summary>
    /// Provider-side channel identifier used for company resolution.
    /// </summary>
    [Description("Provider-side channel identifier used for company resolution.")]
    [DefaultValue("123456789012345")]
    public string ProviderChannelId { get; set; } = string.Empty;

    /// <summary>
    /// Provider-specific metadata as JSON.
    /// </summary>
    [Description("Provider-specific metadata as JSON.")]
    public JsonElement? Metadata { get; set; }

    /// <summary>
    /// Credential reference used by the channel adapter.
    /// </summary>
    [Description("Credential reference used by the channel adapter.")]
    public Guid? CredentialReferenceId { get; set; }
}
