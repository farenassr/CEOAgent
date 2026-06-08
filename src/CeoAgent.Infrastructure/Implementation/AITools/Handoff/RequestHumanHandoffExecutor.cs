using CeoAgent.Application.Abstractions.AI;
using CeoAgent.Infrastructure.Entities.JsonDocuments;
using CeoAgent.Infrastructure.Implementation.AITools.Execution;
using CeoAgent.Shared.AI;
using CeoAgent.Shared.AITools;
using CeoAgent.Shared.Constants;

namespace CeoAgent.Infrastructure.Implementation.AITools.Handoff;

/// <summary>
/// Bridges the request_human_handoff tool call into <see cref="HumanHandoffToolExecutor"/>,
/// reusing the shared validation and idempotent persistence pipeline.
/// </summary>
public sealed class RequestHumanHandoffExecutor(
    HumanHandoffToolExecutor executor,
    ToolExecutionGatewayHelper helper) : IToolExecutor
{
    public string ToolKey => MvpToolKeys.RequestHumanHandoff;

    public async Task<ToolExecutionGatewayResult> ExecuteAsync(
        ToolExecutionGatewayRequest request,
        AgentToolDescriptor descriptor,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        return await helper.ExecuteValidatedAsync<RequestHumanHandoffRequest>(
            request,
            descriptor,
            idempotencyKey,
            ToolExecutionGatewayHelper.IsValid,
            (arguments, token) => executor.RequestHandoffAsync(
                request.CompanyId,
                request.ConversationId,
                descriptor.CompanyToolId,
                request.TriggerMessageId,
                arguments,
                idempotencyKey,
                token),
            cancellationToken);
    }
}
