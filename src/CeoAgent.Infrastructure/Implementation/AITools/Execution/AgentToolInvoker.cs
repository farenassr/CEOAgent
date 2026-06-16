using System.Text.Json;
using CeoAgent.Application.Abstractions.AITools;
using CeoAgent.Infrastructure.Entities;
using CeoAgent.Infrastructure.Persistence.Extensions;
using CeoAgent.Shared.AI;
using CeoAgent.Shared.AITools;
using Microsoft.EntityFrameworkCore;

namespace CeoAgent.Infrastructure.Implementation.AITools.Execution;

public sealed class AgentToolInvoker(
    IAgentToolCatalog catalog,
    CeoAgentDbContext dbContext,
    ToolExecutionGatewayHelper helper) : IAgentToolInvoker
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public async Task<ToolExecutionGatewayResult> ExecuteAsync(
        ToolExecutionGatewayRequest request,
        AgentToolDescriptor descriptor,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        var tool = await ResolveToolAsync(request, cancellationToken);
        if (tool is null)
        {
            return await helper.PersistDeniedAsync(
                request,
                descriptor,
                "tool_not_supported",
                idempotencyKey,
                cancellationToken);
        }

        var requestObject = DeserializeArguments(request.ToolCall.Arguments, tool.RequestType);
        if (requestObject is null || !tool.ValidateObject(requestObject))
        {
            return await helper.PersistDeniedAsync(
                request,
                descriptor,
                "malformed_arguments",
                idempotencyKey,
                cancellationToken);
        }

        var companyTool = await dbContext.CompanyTools
            .AsNoTracking()
            .ForOrganization(request.OrganizationId)
            .Where(entity =>
                entity.Id == descriptor.CompanyToolId
                && entity.ToolKey == request.ToolCall.Name
                && entity.IsEnabled)
            .Select(entity => new
            {
                entity.CredentialReferenceId,
                entity.Configuration,
            })
            .SingleOrDefaultAsync(cancellationToken);
        if (companyTool is null)
        {
            return await helper.PersistDeniedAsync(
                request,
                descriptor,
                "tool_not_enabled",
                idempotencyKey,
                cancellationToken);
        }

        var context = new ToolExecutionContext(
            request.OrganizationId,
            request.ConversationId,
            descriptor.CompanyToolId,
            request.TriggerMessageId,
            idempotencyKey,
            companyTool?.CredentialReferenceId,
            companyTool?.Configuration);

        var execution = (ToolExecution)await tool.ExecuteAsync(context, requestObject, cancellationToken);
        return await helper.ToGatewayResultAsync(request.ToolCall, execution, cancellationToken);
    }

    private async Task<IAgentTool?> ResolveToolAsync(
        ToolExecutionGatewayRequest request,
        CancellationToken cancellationToken)
    {
        var tools = await catalog.GetToolsAsync(
            new AgentToolCatalogContext(request.OrganizationId),
            cancellationToken);

        return tools.SingleOrDefault(tool =>
            string.Equals(tool.ToolKey, request.ToolCall.Name, StringComparison.Ordinal));
    }

    private static object? DeserializeArguments(JsonElement arguments, Type requestType)
    {
        try
        {
            return arguments.Deserialize(requestType, SerializerOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
