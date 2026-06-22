using CeoAgent.Shared.AI;
using CeoAgent.Shared.Enums;

namespace CeoAgent.Infrastructure.Implementation.AI;

internal interface IAgentRuntimeProvider
{
    LlmProvider Provider { get; }

    bool CanEstimateCost(string modelName);

    Task<AgentTurnResult> RunTurnAsync(
        AgentTurnRequest request,
        CancellationToken cancellationToken);
}
