using CeoAgent.Infrastructure.Entities.JsonDocuments;
using CeoAgent.Shared.Enums;

namespace CeoAgent.Infrastructure.Entities;

public sealed class IntegrationCredentialReference : AuditableOrganizationOwnedEntity
{
    /// <summary>
    /// Unique credential reference identifier. Example: 018f4f70-8b5f-7b4c-9d1a-0f6c1d7a2b42.
    /// </summary>
    public Guid Id { get; set; } = Guid.CreateVersion7();

    /// <summary>
    /// Integration provider key. Example: google_calendar.
    /// </summary>
    public IntegrationProvider Provider { get; set; }

    /// <summary>
    /// Purpose for the credential reference. Example: whatsapp_cloud.
    /// </summary>
    public required string Purpose { get; set; }

    /// <summary>
    /// External secret or credential reference. Example: kv://google-calendar/contoso.
    /// </summary>
    public required string Reference { get; set; }

    /// <summary>
    /// Optional provider-specific metadata stored as JSON. Example: {"calendarId":"primary"}.
    /// </summary>
    public CredentialMetadata? Metadata { get; set; }

    /// <summary>
    /// Company that owns this credential reference. Example: Contoso Bistro.
    /// </summary>
    public Company Company { get; set; } = null!;

    /// <summary>
    /// Channels using this credential reference. Example: WhatsApp Cloud channels.
    /// </summary>
    public ICollection<CompanyChannel> CompanyChannels { get; } = [];

    /// <summary>
    /// Tools using this credential reference. Example: Google Calendar tools.
    /// </summary>
    public ICollection<CompanyTool> CompanyTools { get; } = [];
}
