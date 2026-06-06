using CeoAgent.Application.Abstractions.AI;
using CeoAgent.Shared.AI;
using CeoAgent.Shared.AITools;

namespace CeoAgent.Infrastructure.Implementation.AITools.Execution;

public interface IToolExecutor
{
    string ToolKey { get; }
    Task<ToolExecutionGatewayResult> ExecuteAsync(
        ToolExecutionGatewayRequest request,
        AgentToolDescriptor descriptor,
        string idempotencyKey,
        CancellationToken cancellationToken);
}
