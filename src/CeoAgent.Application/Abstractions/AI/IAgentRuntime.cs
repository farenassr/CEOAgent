using CeoAgent.Shared.AI;
using CeoAgent.Shared.Enums;

namespace CeoAgent.Application.Abstractions.AI;

public interface IAgentRuntime
{
    bool CanEstimateCost(LlmProvider provider, string modelName);

    Task<AgentRunResult> RunAsync(AgentRunRequest request, CancellationToken cancellationToken);
}
