using System.ComponentModel.DataAnnotations.Schema;
using CeoAgent.Shared.Enums;

namespace CeoAgent.Infrastructure.Entities;

public sealed class AgentProfile : AuditableOrganizationOwnedEntity
{
    /// <summary>
    /// Unique agent profile identifier. Example: 018f4f70-8b5f-7b4c-9d1a-0f6c1d7a2b32.
    /// </summary>
    public Guid Id { get; set; } = Guid.CreateVersion7();

    /// <summary>
    /// Company-selected model name used by the agent. Example: gpt-4.1-mini.
    /// </summary>
    public required string ModelName { get; set; }

    /// <summary>
    /// Company-selected LLM provider. Not persisted until the operator adds the provider migration.
    /// </summary>
    [NotMapped]
    public LlmProvider LlmProvider { get; set; } = LlmProvider.OpenAI;

    /// <summary>
    /// Display name used when describing the assistant. Example: Contoso Assistant.
    /// </summary>
    public required string DisplayName { get; set; }

    /// <summary>
    /// Default language for assistant replies. Example: es.
    /// </summary>
    public required string Language { get; set; }

    /// <summary>
    /// Optional company-specific prompt instructions. Example: Use a warm but concise tone.
    /// </summary>
    public string? PromptOverride { get; set; }

    /// <summary>
    /// Maximum output tokens requested from the model per runtime call.
    /// </summary>
    public int MaxOutputTokenCount { get; set; } = 1024;

    /// <summary>
    /// Maximum estimated LLM cost allowed for one inbound message job.
    /// </summary>
    public double MaxEstimatedCostUsdPerJob { get; set; } = 0.05d;

    /// <summary>
    /// Company that owns this agent profile. Example: Contoso Bistro.
    /// </summary>
    public Company Company { get; set; } = null!;

    /// <summary>
    /// Conversations created with this profile snapshot. Example: Spanish support conversations.
    /// </summary>
    public ICollection<Conversation> Conversations { get; } = [];
}
