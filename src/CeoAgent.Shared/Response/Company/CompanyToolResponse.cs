using System.Text.Json;

namespace CeoAgent.Shared.Response.Company;

public sealed class CompanyToolResponse
{
    /// <summary>
    /// Unique company tool identifier. Example: 018f4f70-8b5f-7b4c-9d1a-0f6c1d7a2b40.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Company that owns this tool registration. Example: 018f4f70-8b5f-7b4c-9d1a-0f6c1d7a2b30.
    /// </summary>
    public Guid OrganizationId { get; set; }

    /// <summary>
    /// Tool key exposed to the company's agent. Example: check_availability.
    /// </summary>
    public string ToolKey { get; set; } = string.Empty;

    /// <summary>
    /// Human-readable explanation of what this tool does. Example: Checks available reservation slots.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// JSON schema for parameters exposed to the model.
    /// </summary>
    public JsonElement? ParametersSchema { get; set; }

    /// <summary>
    /// Whether the tool is enabled for the company. Example: true.
    /// </summary>
    public bool IsEnabled { get; set; }

    /// <summary>
    /// Optional credential reference used by external-system tools. Example: 018f4f70-8b5f-7b4c-9d1a-0f6c1d7a2b42.
    /// </summary>
    public Guid? CredentialReferenceId { get; set; }

    /// <summary>
    /// Optional company-specific tool configuration as JSON.
    /// </summary>
    public JsonElement? Configuration { get; set; }
}
