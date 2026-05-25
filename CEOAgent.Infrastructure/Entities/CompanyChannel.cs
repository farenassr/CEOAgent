using CeoAgent.Infrastructure.Entities.JsonDocuments;
using CeoAgent.Shared.Enums;

namespace CeoAgent.Infrastructure.Entities;

public sealed class CompanyChannel : AuditableCompanyOwnedEntity
{
    private CompanyChannel()
    {
    }

    /// <summary>
    /// Unique channel registration identifier. Example: 018f4f70-8b5f-7b4c-9d1a-0f6c1d7a2b31.
    /// </summary>
    public Guid Id { get; private set; } = Guid.CreateVersion7();

    /// <summary>
    /// Channel provider key. Example: whatsapp_cloud.
    /// </summary>
    public CompanyChannelProvider Provider { get; private set; }

    /// <summary>
    /// Provider-side channel identifier used for company resolution. Example: 123456789012345.
    /// </summary>
    public string ProviderChannelId { get; private set; } = string.Empty;

    /// <summary>
    /// Provider-specific metadata stored as JSON. Example: {"whatsapp_cloud":{"business_account_id":"987654321"}}.
    /// </summary>
    public ChannelMetadata Metadata { get; private set; } = new();

    /// <summary>
    /// Credential reference used by the channel adapter. Example: 018f4f70-8b5f-7b4c-9d1a-0f6c1d7a2b42.
    /// </summary>
    public Guid? CredentialReferenceId { get; private set; }

    /// <summary>
    /// Company that owns this channel registration. Example: Contoso Bistro.
    /// </summary>
    public Company Company { get; private set; } = null!;

    /// <summary>
    /// Credential reference used by this channel registration. Example: WhatsApp Cloud token reference.
    /// </summary>
    public IntegrationCredentialReference? CredentialReference { get; private set; }

    /// <summary>
    /// Customers whose identities were observed through this channel. Example: WhatsApp customers.
    /// </summary>
    public ICollection<Customer> Customers { get; } = [];

    /// <summary>
    /// Conversations started through this channel. Example: active WhatsApp conversations.
    /// </summary>
    public ICollection<Conversation> Conversations { get; } = [];

    public static CompanyChannel ForWhatsAppCloud(
        Guid companyId,
        string providerChannelId,
        WhatsAppCloudMetadata metadata,
        Guid? credentialReferenceId = null,
        Guid? id = null)
    {
        return new CompanyChannel
        {
            Id = id ?? Guid.CreateVersion7(),
            CompanyId = companyId,
            Provider = CompanyChannelProvider.WhatsAppCloud,
            ProviderChannelId = providerChannelId,
            Metadata = ChannelMetadata.ForWhatsAppCloud(metadata),
            CredentialReferenceId = credentialReferenceId,
        };
    }

    public static CompanyChannel ForInstagram(
        Guid companyId,
        string providerChannelId,
        InstagramMetadata metadata,
        Guid? credentialReferenceId = null,
        Guid? id = null)
    {
        throw new NotSupportedException("Instagram channels are not supported in the MVP.");
    }

    public static CompanyChannel ForTelegram(
        Guid companyId,
        string providerChannelId,
        TelegramMetadata metadata,
        Guid? credentialReferenceId = null,
        Guid? id = null)
    {
        throw new NotSupportedException("Telegram channels are not supported in the MVP.");
    }

    public void UpdateWhatsAppCloud(string providerChannelId, WhatsAppCloudMetadata metadata, Guid? credentialReferenceId)
    {
        EnsureProvider(CompanyChannelProvider.WhatsAppCloud);
        ProviderChannelId = providerChannelId;
        Metadata = ChannelMetadata.ForWhatsAppCloud(metadata);
        CredentialReferenceId = credentialReferenceId;
    }

    public void UpdateInstagram(string providerChannelId, InstagramMetadata metadata, Guid? credentialReferenceId)
    {
        throw new NotSupportedException("Instagram channels are not supported in the MVP.");
    }

    public void UpdateTelegram(string providerChannelId, TelegramMetadata metadata, Guid? credentialReferenceId)
    {
        throw new NotSupportedException("Telegram channels are not supported in the MVP.");
    }

    public TResult Match<TResult>(Func<WhatsAppCloudMetadata, TResult> whatsAppCloud)
    {
        return Provider switch
        {
            CompanyChannelProvider.WhatsAppCloud when Metadata.WhatsAppCloud is { } metadata => whatsAppCloud(metadata),
            CompanyChannelProvider.Instagram => throw new NotSupportedException("Instagram channels are not supported in the MVP."),
            CompanyChannelProvider.Telegram => throw new NotSupportedException("Telegram channels are not supported in the MVP."),
            _ => throw new InvalidOperationException($"Channel provider '{Provider}' does not match its metadata payload."),
        };
    }

    public void Match(Action<WhatsAppCloudMetadata> whatsAppCloud)
    {
        _ = Match(
            metadata =>
            {
                whatsAppCloud(metadata);
                return true;
            });
    }

    private void EnsureProvider(CompanyChannelProvider expectedProvider)
    {
        if (Provider != expectedProvider)
        {
            throw new InvalidOperationException($"Channel provider '{Provider}' cannot be updated as '{expectedProvider}'.");
        }
    }
}
