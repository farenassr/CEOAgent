using System.Text.Json;
using CeoAgent.Shared.Enums;

namespace CeoAgent.Shared.Response.Company;

public sealed class CompanyChannelResponse
{
    /// <summary>
    /// Unique channel registration identifier. Example: 018f4f70-8b5f-7b4c-9d1a-0f6c1d7a2b31.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Company that owns this channel registration. Example: 018f4f70-8b5f-7b4c-9d1a-0f6c1d7a2b30.
    /// </summary>
    public Guid CompanyId { get; set; }

    /// <summary>
    /// Channel provider key. Example: WhatsAppCloud.
    /// </summary>
    public CompanyChannelProvider Provider { get; set; }

    /// <summary>
    /// Provider-side channel identifier used for company resolution. Example: 123456789012345.
    /// </summary>
    public string ProviderChannelId { get; set; } = string.Empty;

    /// <summary>
    /// Credential reference used by the channel adapter. Example: 018f4f70-8b5f-7b4c-9d1a-0f6c1d7a2b42.
    /// </summary>
    public Guid? CredentialReferenceId { get; set; }

    /// <summary>
    /// Provider-specific metadata as JSON.
    /// </summary>
    public JsonElement? Metadata { get; set; }
}
