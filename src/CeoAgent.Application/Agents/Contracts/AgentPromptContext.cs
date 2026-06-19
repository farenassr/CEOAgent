using CeoAgent.Application.Abstractions.AI;

namespace CeoAgent.Application.Agents;

public sealed class AgentPromptContext
{
    public required string CompanyName { get; init; }

    public required string TimeZoneId { get; init; }

    public required DateTimeOffset LocalNow { get; init; }

    public required string AgentDisplayName { get; init; }

    public required string Language { get; init; }

    public required string ModelName { get; init; }

    public string? PromptOverride { get; init; }

    public string? WorkingHoursSummary { get; init; }
}
