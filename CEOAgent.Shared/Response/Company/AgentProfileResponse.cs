namespace CEOAgent.Shared.Response.Company;

public sealed class AgentProfileResponse
{
    /// <summary>
    /// Unique agent profile identifier. Example: 018f4f70-8b5f-7b4c-9d1a-0f6c1d7a2b32.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Company that owns this agent profile. Example: 018f4f70-8b5f-7b4c-9d1a-0f6c1d7a2b30.
    /// </summary>
    public Guid CompanyId { get; set; }

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
    /// Optional company-specific prompt instructions. Example: Use a warm but concise tone.
    /// </summary>
    public string? PromptOverride { get; set; }
}
