using CEOAgent.Infrastructure.Persistence.Entities.Json;

namespace CEOAgent.Infrastructure.Tools;

public sealed record CreateToolExecutionRequest(
    Guid CompanyId,
    Guid ConversationId,
    Guid CompanyToolId,
    Guid TriggerMessageId,
    string ToolKey,
    string IdempotencyKey,
    ToolExecutionRequest? Request);
