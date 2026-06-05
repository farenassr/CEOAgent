using CeoAgent.Integrations.AI;
using CeoAgent.Tools.Models.Execution;

namespace CeoAgent.Tools.Implementation.Execution;

public interface IToolExecutor
{
    string ToolKey { get; }
    Task<ToolExecutionGatewayResult> ExecuteAsync(
        ToolExecutionGatewayRequest request,
        AgentToolDescriptor descriptor,
        string idempotencyKey,
        CancellationToken cancellationToken);
}
