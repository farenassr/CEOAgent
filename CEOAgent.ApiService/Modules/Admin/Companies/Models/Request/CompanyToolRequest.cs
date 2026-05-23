using CEOAgent.Infrastructure.Persistence.Entities.Json;

namespace CEOAgent.ApiService.Modules.Admin.Companies.Models.Request;

public sealed class CompanyToolRequest
{
    /// <summary>
    /// Tool key exposed to the company's agent. Example: check_availability.
    /// </summary>
    public string ToolKey { get; set; } = string.Empty;

    /// <summary>
    /// Whether the tool is enabled for the company. Example: true.
    /// </summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// Optional credential reference used by external-system tools. Example: 018f4f70-8b5f-7b4c-9d1a-0f6c1d7a2b42.
    /// </summary>
    public Guid? CredentialReferenceId { get; set; }

    /// <summary>
    /// Optional company-specific tool configuration as JSON. Example: {"toolKey":"check_availability","maxPartySize":8}.
    /// </summary>
    public ToolConfiguration? Configuration { get; set; }
}
