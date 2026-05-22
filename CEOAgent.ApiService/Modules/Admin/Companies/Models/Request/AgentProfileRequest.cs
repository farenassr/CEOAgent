namespace CEOAgent.ApiService.Modules.Admin.Companies.Models.Request;

public sealed class AgentProfileRequest
{
    /// <summary>
    /// Company-selected model name used by the agent. Example: gpt-4.1-mini.
    /// </summary>
    public string ModelName { get; set; } = string.Empty;

    /// <summary>
    /// Display name used when describing the assistant. Example: Contoso Assistant.
    /// </summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// Default language for assistant replies. Example: es.
    /// </summary>
    public string Language { get; set; } = string.Empty;

    /// <summary>
    /// IANA time zone used for company-local scheduling. Example: America/Bogota.
    /// </summary>
    public string TimeZoneId { get; set; } = string.Empty;

    /// <summary>
    /// Optional company-specific prompt instructions. Example: Use a warm but concise tone.
    /// </summary>
    public string? PromptOverride { get; set; }

    /// <summary>
    /// Working hours configuration as JSON. Example: {"monday":[{"start":"12:00","end":"22:00"}]}.
    /// </summary>
    public string? WorkingHoursJson { get; set; }

}
