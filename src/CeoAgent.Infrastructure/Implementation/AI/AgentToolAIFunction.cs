using System.Text.Json;
using CeoAgent.Application.Abstractions.AITools;
using Microsoft.Extensions.AI;

namespace CeoAgent.Infrastructure.Implementation.AI;

internal sealed class AgentToolAIFunction(
    IAgentTool tool,
    Guid companyToolId,
    string name,
    string description,
    JsonElement jsonSchema,
    bool isMutating) : AIFunction
{
    public IAgentTool Tool { get; } = tool;

    public Guid CompanyToolId { get; } = companyToolId;

    public bool IsMutating { get; } = isMutating;

    public override string Name { get; } = name;

    public override string Description { get; } = description;

    public override JsonElement JsonSchema { get; } = jsonSchema;

    protected override ValueTask<object?> InvokeCoreAsync(
        AIFunctionArguments arguments,
        CancellationToken cancellationToken)
    {
        throw new InvalidOperationException(
            "Agent tools must be invoked through AgentFunctionInvocationGuard.");
    }
}
