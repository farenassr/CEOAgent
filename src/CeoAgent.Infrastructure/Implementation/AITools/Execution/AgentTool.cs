using CeoAgent.Application.Abstractions.AITools;
using CeoAgent.Infrastructure.Entities;

namespace CeoAgent.Infrastructure.Implementation.AITools.Execution;

public abstract class AgentTool<TRequest> : IAgentTool<TRequest>
{
    public abstract string ToolKey { get; }

    public abstract bool IsMutating { get; }

    public abstract string Description { get; }

    public Type RequestType => typeof(TRequest);

    public bool ValidateObject(object request)
    {
        return request is TRequest typedRequest && Validate(typedRequest);
    }

    public virtual bool Validate(TRequest request)
    {
        return true;
    }

    public async Task<IAgentToolExecution> ExecuteAsync(
        ToolExecutionContext context,
        object request,
        CancellationToken cancellationToken)
    {
        return await ExecuteToolAsync(context, (TRequest)request, cancellationToken);
    }

    public async Task<IAgentToolExecution> ExecuteAsync(
        ToolExecutionContext context,
        TRequest request,
        CancellationToken cancellationToken)
    {
        return await ExecuteToolAsync(context, request, cancellationToken);
    }

    protected abstract Task<ToolExecution> ExecuteToolAsync(
        ToolExecutionContext context,
        TRequest request,
        CancellationToken cancellationToken);
}
