namespace CEOAgent.ApiService.Modules.Admin.Companies.Models.Request;

public sealed class CompanyChannelRequest
{
    /// <summary>
    /// Channel provider key. Example: whatsapp_cloud.
    /// </summary>
    public string Provider { get; set; } = string.Empty;

    /// <summary>
    /// Provider-side channel identifier used for company resolution. Example: 123456789012345.
    /// </summary>
    public string ProviderChannelId { get; set; } = string.Empty;

    /// <summary>
    /// Provider-specific metadata as JSON. Example: {"business_account_id":"987654321"}.
    /// </summary>
    public string? MetadataJson { get; set; }

    /// <summary>
    /// Secret or credential reference used by the channel adapter. Example: kv://whatsapp/contoso.
    /// </summary>
    public string? CredentialReference { get; set; }
}
