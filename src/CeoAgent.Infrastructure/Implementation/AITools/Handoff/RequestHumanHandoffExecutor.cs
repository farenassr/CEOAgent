using CeoAgent.Application.Abstractions.AITools;
using CeoAgent.Infrastructure.Entities;
using CeoAgent.Infrastructure.Entities.JsonDocuments;
using CeoAgent.Infrastructure.Implementation.AITools.Execution;
using CeoAgent.Shared.Constants;

namespace CeoAgent.Infrastructure.Implementation.AITools.Handoff;

public sealed class RequestHumanHandoffTool(
    HumanHandoffToolExecutor executor) : AgentTool<RequestHumanHandoffRequest>
{
    public override string ToolKey => MvpToolKeys.RequestHumanHandoff;

    public override bool IsMutating => true;

    public override string Description => "Escalate the conversation to a human agent when the customer asks for a person or automation cannot continue.";

    public override bool Validate(RequestHumanHandoffRequest request)
    {
        return !string.IsNullOrWhiteSpace(request.Reason);
    }

    protected override async Task<ToolExecution> ExecuteToolAsync(
        ToolExecutionContext context,
        RequestHumanHandoffRequest request,
        CancellationToken cancellationToken)
    {
        return await executor.RequestHandoffAsync(context, request, cancellationToken);
    }
}
