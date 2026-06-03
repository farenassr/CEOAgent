namespace CeoAgent.Integrations.AI;

public interface IAgentRuntime
{
    Task<AgentRunResult> RunAsync(AgentRunRequest request, CancellationToken cancellationToken);
}
