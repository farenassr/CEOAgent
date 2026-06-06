using CeoAgent.Shared.AI;

namespace CeoAgent.Application.Abstractions.AI;

public interface IAgentRuntime
{
    Task<AgentRunResult> RunAsync(AgentRunRequest request, CancellationToken cancellationToken);
}
