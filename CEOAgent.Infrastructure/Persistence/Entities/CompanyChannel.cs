namespace CEOAgent.Infrastructure.Persistence.Entities;

public sealed class CompanyChannel : AuditableCompanyOwnedEntity
{
    /// <summary>
    /// Unique channel registration identifier. Example: 018f4f70-8b5f-7b4c-9d1a-0f6c1d7a2b31.
    /// </summary>
    public Guid Id { get; set; } = Guid.CreateVersion7();

    /// <summary>
    /// Channel provider key. Example: whatsapp_cloud.
    /// </summary>
    public required string Provider { get; set; }

    /// <summary>
    /// Provider-side channel identifier used for company resolution. Example: 123456789012345.
    /// </summary>
    public required string ProviderChannelId { get; set; }

    /// <summary>
    /// Provider-specific metadata stored as JSON. Example: {"business_account_id":"987654321"}.
    /// </summary>
    public string? MetadataJson { get; set; }

    /// <summary>
    /// Secret or credential reference used by the channel adapter. Example: kv://whatsapp/contoso.
    /// </summary>
    public string? CredentialReference { get; set; }

    /// <summary>
    /// Company that owns this channel registration. Example: Contoso Bistro.
    /// </summary>
    public Company Company { get; set; } = null!;
}
