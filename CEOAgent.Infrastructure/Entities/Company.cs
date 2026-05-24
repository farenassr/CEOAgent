using CEOAgent.Shared.Enums;
using CEOAgent.Infrastructure.Entities.JsonDocuments;

namespace CEOAgent.Infrastructure.Entities;

public sealed class Company
{
    /// <summary>
    /// Unique company identifier. Example: 018f4f70-8b5f-7b4c-9d1a-0f6c1d7a2b30.
    /// </summary>
    public Guid Id { get; set; } = Guid.CreateVersion7();

    /// <summary>
    /// Human-readable company name. Example: Contoso Bistro.
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// Working hours configuration stored as JSON. Example: {"schedule":{"monday":[{"start":"12:00","end":"22:00"}]}}.
    /// </summary>
    public WorkingHours? WorkingHours { get; set; }

    /// <summary>
    /// IANA time zone used for company-local scheduling. Example: America/Bogota.
    /// </summary>
    public required string TimeZoneId { get; set; }

    /// <summary>
    /// Current company lifecycle status. Example: Active.
    /// </summary>
    public CompanyStatus Status { get; set; } = CompanyStatus.Active;

    /// <summary>
    /// UTC timestamp when the company was created. Example: 2026-05-22T10:15:30Z.
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// UTC timestamp when the company was last updated. Example: 2026-05-22T10:45:00Z.
    /// </summary>
    public DateTime UpdatedAt { get; set; }

    /// <summary>
    /// Channel registrations that resolve inbound messages to this company. Example: one WhatsApp Cloud channel.
    /// </summary>
    public ICollection<CompanyChannel> Channels { get; } = new List<CompanyChannel>();

    /// <summary>
    /// Agent configuration used for this company's conversations. Example: a Spanish support assistant profile.
    /// </summary>
    public AgentProfile? AgentProfile { get; set; }

    /// <summary>
    /// Tools enabled for this company. Example: request_human_handoff.
    /// </summary>
    public ICollection<CompanyTool> Tools { get; } = new List<CompanyTool>();

    /// <summary>
    /// External integration credential references owned by this company. Example: google_calendar primary credential.
    /// </summary>
    public ICollection<IntegrationCredentialReference> IntegrationCredentials { get; } = new List<IntegrationCredentialReference>();
}
