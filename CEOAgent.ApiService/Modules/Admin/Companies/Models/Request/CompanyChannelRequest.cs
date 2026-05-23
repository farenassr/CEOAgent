using CEOAgent.Infrastructure.Persistence.Entities.Json;

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
    /// Provider-specific metadata as JSON. Example: {"businessAccountId":"987654321"}.
    /// </summary>
    public ChannelMetadata? Metadata { get; set; }

    /// <summary>
    /// Credential reference used by the channel adapter. Example: 018f4f70-8b5f-7b4c-9d1a-0f6c1d7a2b42.
    /// </summary>
    public Guid? CredentialReferenceId { get; set; }
}
