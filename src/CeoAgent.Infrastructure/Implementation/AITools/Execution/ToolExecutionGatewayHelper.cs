using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CeoAgent.Infrastructure;
using CeoAgent.Infrastructure.Entities;
using CeoAgent.Infrastructure.Entities.JsonDocuments;
using CeoAgent.Infrastructure.Persistence.Extensions;
using CeoAgent.Shared.AITools;
using CeoAgent.Application.Abstractions.AI;
using CeoAgent.Shared.AI;
using CeoAgent.Shared.Enums;
using Microsoft.EntityFrameworkCore;

namespace CeoAgent.Infrastructure.Implementation.AITools.Execution;

public sealed class ToolExecutionGatewayHelper(CeoAgentDbContext dbContext)
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public async Task<ToolExecutionGatewayResult> PersistDeniedAsync(
        ToolExecutionGatewayRequest request,
        AgentToolDescriptor descriptor,
        string failureReason,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        var existing = await dbContext.FindTrackedOrPersistedToolExecutionAsync(
            request.OrganizationId,
            idempotencyKey,
            cancellationToken);
        if (existing is not null)
        {
            return await ToGatewayResultAsync(request.ToolCall, existing, cancellationToken);
        }

        var execution = new ToolExecution
        {
            OrganizationId = request.OrganizationId,
            ConversationId = request.ConversationId,
            CompanyToolId = descriptor.CompanyToolId,
            TriggerMessageId = request.TriggerMessageId,
            ToolKey = request.ToolCall.Name,
            IdempotencyKey = idempotencyKey,
            Status = ToolExecutionStatus.ToolExecutionDenied,
            FailureReason = failureReason,
        };

        dbContext.ToolExecutions.Add(execution);
        return await ToGatewayResultAsync(request.ToolCall, execution, cancellationToken);
    }

    public async Task<ToolExecutionGatewayResult> PersistToolNotEnabledDeniedAsync(
        ToolExecutionGatewayRequest request,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        var existing = await dbContext.FindTrackedOrPersistedToolExecutionAsync(
            request.OrganizationId,
            idempotencyKey,
            cancellationToken);
        if (existing is not null)
        {
            return await ToGatewayResultAsync(request.ToolCall, existing, cancellationToken);
        }

        var companyTool = await dbContext.CompanyTools
            .AsNoTracking()
            .SingleOrDefaultAsync(
                tool => tool.OrganizationId == request.OrganizationId
                    && tool.ToolKey == request.ToolCall.Name,
                cancellationToken);
        if (companyTool is null)
        {
            return Denied(request.ToolCall, "tool_not_enabled");
        }

        var execution = new ToolExecution
        {
            OrganizationId = request.OrganizationId,
            ConversationId = request.ConversationId,
            CompanyToolId = companyTool.Id,
            TriggerMessageId = request.TriggerMessageId,
            ToolKey = request.ToolCall.Name,
            IdempotencyKey = idempotencyKey,
            Status = ToolExecutionStatus.ToolExecutionDenied,
            FailureReason = "tool_not_enabled",
        };

        dbContext.ToolExecutions.Add(execution);
        return await ToGatewayResultAsync(request.ToolCall, execution, cancellationToken);
    }

    public async Task<ToolExecutionGatewayResult> ToGatewayResultAsync(
        AgentToolCall toolCall,
        ToolExecution execution,
        CancellationToken cancellationToken)
    {
        var content = JsonSerializer.Serialize(new
        {
            toolKey = execution.ToolKey,
            status = ToWireStatus(execution.Status),
            failureReason = execution.FailureReason,
            result = execution.Result,
        }, SerializerOptions);

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

        return new ToolExecutionGatewayResult(toolCall.Id, toolCall.Name, content);
    }

    public static string CreateIdempotencyKey(ToolExecutionGatewayRequest request)
    {
        var canonicalArguments = request.ToolCall.Arguments.GetRawText();
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(canonicalArguments));
        var hash = Convert.ToHexString(hashBytes);
        return $"{request.ConversationId:N}:{request.InboundMessageId:N}:{request.ToolCall.Name}:{hash[..16]}";
    }

    private static ToolExecutionGatewayResult Denied(AgentToolCall toolCall, string failureReason)
    {
        var content = JsonSerializer.Serialize(new
        {
            toolKey = toolCall.Name,
            status = "denied",
            failureReason,
        }, SerializerOptions);

        return new ToolExecutionGatewayResult(toolCall.Id, toolCall.Name, content);
    }

    private static string ToWireStatus(ToolExecutionStatus status)
    {
        return status switch
        {
            ToolExecutionStatus.ToolExecutionSucceeded => "succeeded",
            ToolExecutionStatus.ToolExecutionDenied => "denied",
            ToolExecutionStatus.ToolExecutionFailed => "failed",
            ToolExecutionStatus.ToolExecutionInProgress => "in_progress",
            ToolExecutionStatus.ToolExecutionRetryScheduled => "retry_scheduled",
            _ => "waiting_to_run",
        };
    }
}
