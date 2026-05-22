namespace CEOAgent.Infrastructure.Persistence.Entities;

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
    /// Optional company-specific tool configuration stored as JSON. Example: {"maxPartySize":8}.
    /// </summary>
    public string? ConfigurationJson { get; set; }

    /// <summary>
    /// Company that owns this tool registration. Example: Contoso Bistro.
    /// </summary>
    public Company Company { get; set; } = null!;
}
