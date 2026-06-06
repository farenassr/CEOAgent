using CeoAgent.Shared.AI;

namespace CeoAgent.Shared.AITools;

public sealed record ToolExecutionGatewayRequest(
    Guid CompanyId,
    Guid ConversationId,
    Guid TriggerMessageId,
    Guid InboundMessageId,
    AgentToolCall ToolCall,
    IReadOnlyList<AgentToolDescriptor> EnabledTools,
    bool SideEffectsEnabled = true);
