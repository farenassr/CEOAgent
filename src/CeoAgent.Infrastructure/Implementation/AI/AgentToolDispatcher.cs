using System.Text.Json;
using CeoAgent.Application.Abstractions.AITools;
using CeoAgent.Infrastructure.Entities;
using CeoAgent.Infrastructure.Entities.JsonDocuments;
using CeoAgent.Infrastructure.Persistence.Extensions;
using CeoAgent.Shared.Enums;
using Microsoft.EntityFrameworkCore;

namespace CeoAgent.Infrastructure.Implementation.AI;

public sealed class AgentToolDispatcher(
    CeoAgentDbContext dbContext,
    IAgentToolCatalog toolCatalog,
    TimeProvider timeProvider)
{
    private const string MutatingToolsDisabledFailureReason = "mutating_tools_disabled";
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    internal async Task<AgentToolDispatchResult> DispatchAsync(
        AgentTurnContext turnContext,
        string functionName,
        JsonElement arguments,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        var existing = await dbContext.FindTrackedOrPersistedToolExecutionAsync(
            turnContext.OrganizationId,
            idempotencyKey,
            cancellationToken);
        if (existing is not null)
        {
            var existingContent = await ToToolResultContentAsync(existing, cancellationToken);
            return AgentToolDispatchResult.FromExecution(existingContent, existing);
        }

        var companyTool = await LoadCompanyToolAsync(
            turnContext.OrganizationId,
            functionName,
            cancellationToken);
        var triggerMessage = AddToolCallMessage(turnContext, functionName);
        if (companyTool is null)
        {
            return await PersistDeniedMessageAuditAsync(
                turnContext,
                functionName,
                "tool_not_enabled",
                cancellationToken);
        }

        if (!companyTool.IsEnabled)
        {
            return await PersistDeniedAsync(
                turnContext,
                triggerMessage.Id,
                companyTool.Id,
                functionName,
                idempotencyKey,
                "tool_not_enabled",
                cancellationToken);
        }

        var tool = await ResolveToolAsync(turnContext.OrganizationId, functionName, cancellationToken);
        if (tool is null)
        {
            return await PersistDeniedAsync(
                turnContext,
                triggerMessage.Id,
                companyTool.Id,
                functionName,
                idempotencyKey,
                "tool_not_supported",
                cancellationToken);
        }

        if (tool.IsMutating && !turnContext.MutatingToolsEnabled)
        {
            return await PersistDeniedAsync(
                turnContext,
                triggerMessage.Id,
                companyTool.Id,
                functionName,
                idempotencyKey,
                turnContext.MutatingToolsDisabledReason ?? MutatingToolsDisabledFailureReason,
                cancellationToken);
        }

        var requestObject = DeserializeArguments(arguments, tool.RequestType);
        if (requestObject is null || !tool.ValidateObject(requestObject))
        {
            return await PersistDeniedAsync(
                turnContext,
                triggerMessage.Id,
                companyTool.Id,
                functionName,
                idempotencyKey,
                "malformed_arguments",
                cancellationToken);
        }

        var executionContext = new ToolExecutionContext(
            turnContext.OrganizationId,
            turnContext.ConversationId,
            companyTool.Id,
            triggerMessage.Id,
            idempotencyKey,
            companyTool.CredentialReferenceId,
            companyTool.Configuration);

        var execution = (ToolExecution)await tool.ExecuteAsync(
            executionContext,
            requestObject,
            cancellationToken);
        var content = await ToToolResultContentAsync(execution, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        return AgentToolDispatchResult.FromExecution(content, execution);
    }

    private async Task<CompanyToolPolicy?> LoadCompanyToolAsync(
        Guid organizationId,
        string functionName,
        CancellationToken cancellationToken)
    {
        return await dbContext.CompanyTools
            .AsNoTracking()
            .ForOrganization(organizationId)
            .Where(entity => entity.ToolKey == functionName)
            .Select(entity => new CompanyToolPolicy(
                entity.Id,
                entity.IsEnabled,
                entity.CredentialReferenceId,
                entity.Configuration))
            .SingleOrDefaultAsync(cancellationToken);
    }

    private Message AddToolCallMessage(AgentTurnContext turnContext, string functionName)
    {
        var triggerMessage = new Message
        {
            OrganizationId = turnContext.OrganizationId,
            ConversationId = turnContext.ConversationId,
            Role = MessageRole.ToolCall,
            Type = MessageType.Text,
            MessageText = functionName,
            OccurredAt = timeProvider.GetUtcNow().UtcDateTime,
        };
        dbContext.Messages.Add(triggerMessage);
        return triggerMessage;
    }

    private async Task<IAgentTool?> ResolveToolAsync(
        Guid organizationId,
        string functionName,
        CancellationToken cancellationToken)
    {
        var tools = await toolCatalog.GetToolsAsync(
            new AgentToolCatalogContext(organizationId),
            cancellationToken);

        return tools.SingleOrDefault(tool =>
            string.Equals(tool.ToolKey, functionName, StringComparison.Ordinal));
    }

    private async Task<AgentToolDispatchResult> PersistDeniedAsync(
        AgentTurnContext turnContext,
        Guid triggerMessageId,
        Guid companyToolId,
        string functionName,
        string idempotencyKey,
        string failureReason,
        CancellationToken cancellationToken)
    {
        var execution = new ToolExecution
        {
            OrganizationId = turnContext.OrganizationId,
            ConversationId = turnContext.ConversationId,
            CompanyToolId = companyToolId,
            TriggerMessageId = triggerMessageId,
            ToolKey = functionName,
            IdempotencyKey = idempotencyKey,
            Status = ToolExecutionStatus.ToolExecutionDenied,
            FailureReason = failureReason,
        };
        dbContext.ToolExecutions.Add(execution);

        var content = AgentToolResultContent.Serialize(execution);
        AddToolResultMessage(turnContext, execution, content);

        await dbContext.SaveChangesAsync(cancellationToken);
        return AgentToolDispatchResult.DeniedWithExecution(content, execution);
    }

    private async Task<AgentToolDispatchResult> PersistDeniedMessageAuditAsync(
        AgentTurnContext turnContext,
        string functionName,
        string failureReason,
        CancellationToken cancellationToken)
    {
        var content = AgentToolResultContent.SerializeDenied(functionName, failureReason);
        var resultMessage = new Message
        {
            OrganizationId = turnContext.OrganizationId,
            ConversationId = turnContext.ConversationId,
            Role = MessageRole.ToolResult,
            Type = MessageType.Text,
            MessageText = content,
            OccurredAt = timeProvider.GetUtcNow().UtcDateTime,
        };
        dbContext.Messages.Add(resultMessage);

        await dbContext.SaveChangesAsync(cancellationToken);
        return AgentToolDispatchResult.DeniedWithoutExecution(content, failureReason);
    }

    private async Task<string> ToToolResultContentAsync(
        ToolExecution execution,
        CancellationToken cancellationToken)
    {
        var content = AgentToolResultContent.Serialize(execution);
        if (execution.ResultMessageId is { } resultMessageId)
        {
            var resultMessage = await dbContext.FindTrackedOrPersistedMessageAsync(
                execution.OrganizationId,
                resultMessageId,
                cancellationToken);

            if (resultMessage is not null && resultMessage.MessageText != content)
            {
                resultMessage.MessageText = content;
            }
        }

        return content;
    }

    private void AddToolResultMessage(
        AgentTurnContext turnContext,
        ToolExecution execution,
        string content)
    {
        var resultMessage = new Message
        {
            OrganizationId = turnContext.OrganizationId,
            ConversationId = turnContext.ConversationId,
            Role = MessageRole.ToolResult,
            Type = MessageType.Text,
            MessageText = content,
            OccurredAt = timeProvider.GetUtcNow().UtcDateTime,
        };
        dbContext.Messages.Add(resultMessage);
        execution.ResultMessageId = resultMessage.Id;
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

    private sealed record CompanyToolPolicy(
        Guid Id,
        bool IsEnabled,
        Guid? CredentialReferenceId,
        ToolConfiguration? Configuration);
}
