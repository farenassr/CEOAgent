namespace CeoAgent.Application.Agents;

public interface IAgentRuntime
{
    Task<AgentRunResult> RunAsync(AgentRunRequest request, CancellationToken cancellationToken);
}
