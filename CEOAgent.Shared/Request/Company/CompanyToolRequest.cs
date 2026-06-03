using System.ComponentModel;
using System.Text.Json;

namespace CeoAgent.Shared.Request.Company;

public sealed class CompanyToolRequest
{
    /// <summary>
    /// Tool key exposed to the company's agent.
    /// </summary>
    [Description("Tool key exposed to the company's agent.")]
    [DefaultValue("check_availability")]
    public string ToolKey { get; set; } = string.Empty;

    /// <summary>
    /// Human-readable explanation of what this tool does.
    /// </summary>
    [Description("Human-readable explanation of what this tool does.")]
    [DefaultValue("Checks available reservation slots.")]
    public string? Description { get; set; }

    /// <summary>
    /// JSON schema for parameters exposed to the model.
    /// </summary>
    [Description("JSON schema for parameters exposed to the model.")]
    public JsonElement? ParametersSchema { get; set; }

    /// <summary>
    /// Whether the tool is enabled for the company.
    /// </summary>
    [Description("Whether the tool is enabled for the company.")]
    [DefaultValue(true)]
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// Optional credential reference used by external-system tools.
    /// </summary>
    [Description("Optional credential reference used by external-system tools.")]
    public Guid? CredentialReferenceId { get; set; }

    /// <summary>
    /// Optional company-specific tool configuration as JSON.
    /// </summary>
    [Description("Optional company-specific tool configuration as JSON.")]
    public JsonElement? Configuration { get; set; }
}
