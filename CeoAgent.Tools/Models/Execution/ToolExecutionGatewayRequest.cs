using CeoAgent.Integrations.AI;

namespace CeoAgent.Tools.Models.Execution;

public sealed record ToolExecutionGatewayRequest(
    Guid CompanyId,
    Guid ConversationId,
    Guid TriggerMessageId,
    Guid InboundMessageId,
    AgentToolCall ToolCall,
    IReadOnlyList<AgentToolDescriptor> EnabledTools);
