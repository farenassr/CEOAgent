using CeoAgent.Infrastructure.Entities.JsonDocuments;

namespace CeoAgent.Infrastructure.Entities;

public sealed class CompanyTool : AuditableCompanyOwnedEntity
{
    /// <summary>
    /// Unique company tool identifier. Example: 018f4f70-8b5f-7b4c-9d1a-0f6c1d7a2b40.
    /// </summary>
    public Guid Id { get; set; } = Guid.CreateVersion7();

    /// <summary>
    /// Tool key exposed to the company's agent. Example: check_availability.
    /// </summary>
    public required string ToolKey { get; set; }

    /// <summary>
    /// Whether the tool is enabled for the company. Example: true.
    /// </summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// Optional credential reference used by external-system tools. Example: 018f4f70-8b5f-7b4c-9d1a-0f6c1d7a2b42.
    /// </summary>
    public Guid? CredentialReferenceId { get; set; }

    /// <summary>
    /// Optional company-specific tool configuration stored as JSON. Example: {"toolKey":"check_availability","maxPartySize":8}.
    /// </summary>
    public ToolConfiguration? Configuration { get; set; }

    /// <summary>
    /// Company that owns this tool registration. Example: Contoso Bistro.
    /// </summary>
    public Company Company { get; set; } = null!;

    /// <summary>
    /// Optional credential reference used by this tool. Example: Google Calendar OAuth reference.
    /// </summary>
    public IntegrationCredentialReference? CredentialReference { get; set; }

    /// <summary>
    /// Executions recorded for this enabled tool. Example: check_availability calls.
    /// </summary>
    public ICollection<ToolExecution> ToolExecutions { get; } = [];
}
