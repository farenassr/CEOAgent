using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CeoAgent.Infrastructure;
using CeoAgent.Infrastructure.Entities;
using CeoAgent.Infrastructure.Entities.JsonDocuments;
using CeoAgent.Tools.Models.Execution;
using CeoAgent.Integrations.AI;
using CeoAgent.Shared.Enums;
using Microsoft.EntityFrameworkCore;

namespace CeoAgent.Tools.Implementation.Execution;

public sealed class ToolExecutionGatewayHelper(CeoAgentDbContext dbContext)
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public static bool TryDeserialize<TRequest>(JsonElement arguments, out TRequest request)
        where TRequest : class
    {
        try
        {
            request = arguments.Deserialize<TRequest>(SerializerOptions)
                ?? throw new JsonException("Tool arguments were null.");
            return true;
        }
        catch (JsonException)
        {
            request = null!;
            return false;
        }
    }

    public static bool IsValid(CheckAvailabilityRequest request)
    {
        return request.Date != default
            && request.PartySize > 0;
    }

    public static bool IsValid(CreateCalendarEventRequest request)
    {
        return request.Start != default
            && request.End != default
            && request.End > request.Start
            && !string.IsNullOrWhiteSpace(request.Summary);
    }

    public async Task<ToolExecutionGatewayResult> PersistDeniedAsync(
        ToolExecutionGatewayRequest request,
        AgentToolDescriptor descriptor,
        string failureReason,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        var existing = await dbContext.ToolExecutions
            .Where(entity => entity.CompanyId == request.CompanyId && entity.IdempotencyKey == idempotencyKey)
            .SingleOrDefaultAsync(cancellationToken);
        if (existing is not null)
        {
            return await ToGatewayResultAsync(request.ToolCall, existing, cancellationToken);
        }

        var execution = new ToolExecution
        {
            CompanyId = request.CompanyId,
            ConversationId = request.ConversationId,
            CompanyToolId = descriptor.CompanyToolId,
            TriggerMessageId = request.TriggerMessageId,
            ToolKey = request.ToolCall.Name,
            IdempotencyKey = idempotencyKey,
            Status = ToolExecutionStatus.Denied,
            FailureReason = failureReason,
        };

        dbContext.ToolExecutions.Add(execution);
        await dbContext.SaveChangesAsync(cancellationToken);
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
            status = execution.Status.ToString().ToLowerInvariant(),
            failureReason = execution.FailureReason,
            result = execution.Result,
        }, SerializerOptions);

        if (execution.ResultMessageId is { } resultMessageId)
        {
            var resultMessage = await dbContext.Messages
                .Where(entity => entity.Id == resultMessageId && entity.CompanyId == execution.CompanyId)
                .FirstOrDefaultAsync(cancellationToken);
            if (resultMessage is not null && resultMessage.MessageText != content)
            {
                resultMessage.MessageText = content;
                await dbContext.SaveChangesAsync(cancellationToken);
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
}
