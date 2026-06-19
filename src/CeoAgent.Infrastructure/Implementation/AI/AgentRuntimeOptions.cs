namespace CeoAgent.Infrastructure.Implementation.AI;

public sealed class AgentRuntimeOptions
{
    public const string SectionName = "AgentRuntime";

    public int SessionIdleExpirationHours { get; set; } = 48;

    public int MaxSessionTurns { get; set; } = 40;

    public int MaximumToolIterationsPerRequest { get; set; } = 4;

    public bool AllowMultipleToolCalls { get; set; }

    public bool AllowConcurrentToolInvocation { get; set; }
}
