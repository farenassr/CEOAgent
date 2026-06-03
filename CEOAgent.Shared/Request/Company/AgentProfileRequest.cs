using System.ComponentModel;
using System.Text.Json;
using CeoAgent.Shared.Enums;

namespace CeoAgent.Shared.Request.Company;

public sealed class AgentProfileRequest
{
    /// <summary>
    /// Company-selected model name used by the agent.
    /// </summary>
    [Description("Company-selected model name used by the agent.")]
    [DefaultValue("gpt-4.1-mini")]
    public string ModelName { get; set; } = string.Empty;

    /// <summary>
    /// Company-selected LLM provider used by the agent runtime.
    /// </summary>
    [Description("Company-selected LLM provider used by the agent runtime.")]
    [DefaultValue(LlmProvider.OpenAI)]
    public LlmProvider LlmProvider { get; set; } = LlmProvider.OpenAI;

    /// <summary>
    /// Display name used when describing the assistant.
    /// </summary>
    [Description("Display name used when describing the assistant.")]
    [DefaultValue("Contoso Assistant")]
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// Default language for assistant replies.
    /// </summary>
    [Description("Default language for assistant replies.")]
    [DefaultValue("es")]
    public string Language { get; set; } = string.Empty;

    /// <summary>
    /// IANA time zone used for company-local scheduling.
    /// </summary>
    [Description("IANA time zone used for company-local scheduling.")]
    [DefaultValue("America/Bogota")]
    public string TimeZoneId { get; set; } = string.Empty;

    /// <summary>
    /// Optional company-specific prompt instructions.
    /// </summary>
    [Description("Optional company-specific prompt instructions.")]
    [DefaultValue("Use a warm but concise tone.")]
    public string? PromptOverride { get; set; }

    /// <summary>
    /// Working hours configuration as JSON.
    /// </summary>
    [Description("Working hours configuration as JSON.")]
    public JsonElement? WorkingHours { get; set; }
}
