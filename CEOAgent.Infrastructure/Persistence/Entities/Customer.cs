namespace CEOAgent.Infrastructure.Persistence.Entities;

public sealed class Customer : AuditableCompanyOwnedEntity
{
    /// <summary>
    /// Unique customer identifier. Example: 018f4f70-8b5f-7b4c-9d1a-0f6c1d7a2b33.
    /// </summary>
    public Guid Id { get; set; } = Guid.CreateVersion7();

    /// <summary>
    /// Channel type where the customer identity was observed. Example: whatsapp_cloud.
    /// </summary>
    public required string ChannelType { get; set; }

    /// <summary>
    /// Provider-side customer identifier within the company and channel. Example: 573001112233.
    /// </summary>
    public required string ExternalCustomerId { get; set; }

    /// <summary>
    /// Optional display name supplied by the channel or staff. Example: Karina Perez.
    /// </summary>
    public string? DisplayName { get; set; }

    /// <summary>
    /// Conversations associated with this customer. Example: the current open WhatsApp conversation.
    /// </summary>
    public ICollection<Conversation> Conversations { get; } = [];
}
