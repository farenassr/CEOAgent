using CEOAgent.Infrastructure.Persistence.Entities.Json;

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
    /// Provider-specific metadata stored as JSON. Example: {"businessAccountId":"987654321"}.
    /// </summary>
    public ChannelMetadata? Metadata { get; set; }

    /// <summary>
    /// Credential reference used by the channel adapter. Example: 018f4f70-8b5f-7b4c-9d1a-0f6c1d7a2b42.
    /// </summary>
    public Guid? CredentialReferenceId { get; set; }

    /// <summary>
    /// Company that owns this channel registration. Example: Contoso Bistro.
    /// </summary>
    public Company Company { get; set; } = null!;

    /// <summary>
    /// Credential reference used by this channel registration. Example: WhatsApp Cloud token reference.
    /// </summary>
    public IntegrationCredentialReference? CredentialReference { get; set; }

    /// <summary>
    /// Customers whose identities were observed through this channel. Example: WhatsApp customers.
    /// </summary>
    public ICollection<Customer> Customers { get; } = new List<Customer>();

    /// <summary>
    /// Conversations started through this channel. Example: active WhatsApp conversations.
    /// </summary>
    public ICollection<Conversation> Conversations { get; } = new List<Conversation>();
}
