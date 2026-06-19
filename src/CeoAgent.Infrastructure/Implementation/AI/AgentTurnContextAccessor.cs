using CeoAgent.Shared.Enums;

namespace CeoAgent.Infrastructure.Implementation.AI;

public sealed class AgentTurnContextAccessor
{
    public AgentTurnContext? Current { get; private set; }

    public void Set(AgentTurnContext context)
    {
        Current = context;
    }

    public void Clear()
    {
        Current = null;
    }
}

public sealed class AgentTurnContext
{
    public required Guid OrganizationId { get; init; }

    public required Guid ConversationId { get; init; }

    public required Guid InboundMessageId { get; init; }

    public required LlmProvider Provider { get; init; }

    public required string ModelName { get; init; }

    public string? CorrelationId { get; init; }

    public bool MutatingToolsEnabled { get; init; } = true;

    public string? MutatingToolsDisabledReason { get; init; }

    public int ToolInvocationCount { get; private set; }

    public void RecordToolInvocation()
    {
        ToolInvocationCount++;
    }
}
