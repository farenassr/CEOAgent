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
    /// Optional company-specific tool configuration as JSON. Example: {"maxPartySize":8}.
    /// </summary>
    public string? ConfigurationJson { get; set; }
}
