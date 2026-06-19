using System.Text.Json;

namespace CeoAgent.Application.Abstractions.AITools;

public interface IAgentToolExecution
{
}

public interface IAgentTool
{
    string ToolKey { get; }

    bool IsMutating { get; }

    string Description { get; }

    JsonElement ParametersSchema { get; }

    Type RequestType { get; }

    bool ValidateObject(object request);

    Task<IAgentToolExecution> ExecuteAsync(
        ToolExecutionContext context,
        object request,
        CancellationToken cancellationToken);
}

public interface IAgentTool<TRequest> : IAgentTool
{
    bool Validate(TRequest request);

    Task<IAgentToolExecution> ExecuteAsync(
        ToolExecutionContext context,
        TRequest request,
        CancellationToken cancellationToken);
}

public sealed record ToolExecutionContext(
    Guid OrganizationId,
    Guid ConversationId,
    Guid CompanyToolId,
    Guid TriggerMessageId,
    string IdempotencyKey,
    Guid? CredentialReferenceId = null,
    object? Configuration = null);

public sealed record AgentToolCatalogContext(
    Guid OrganizationId,
    IReadOnlyDictionary<string, object?>? TenantMetadata = null);

public interface IAgentToolCatalog
{
    Task<IReadOnlyList<IAgentTool>> GetToolsAsync(
        AgentToolCatalogContext context,
        CancellationToken cancellationToken);
}

public interface IDynamicAgentToolProvider
{
    Task<IReadOnlyList<IAgentTool>> GetToolsAsync(
        AgentToolCatalogContext context,
        CancellationToken cancellationToken);
}
