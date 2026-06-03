using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CeoAgent.Infrastructure;
using CeoAgent.Infrastructure.Entities;
using CeoAgent.Infrastructure.Entities.JsonDocuments;
using CeoAgent.Integrations.AI;
using CeoAgent.Shared.Constants;
using CeoAgent.Shared.Enums;
using CeoAgent.Tools.Implementation.GoogleCalendar;
using CeoAgent.Tools.Models.Execution;
using Microsoft.EntityFrameworkCore;

namespace CeoAgent.Tools.Implementation.Execution;

public sealed class ToolExecutionGateway(
    CeoAgentDbContext dbContext,
    GoogleCalendarToolExecutor googleCalendarToolExecutor)
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public async Task<ToolExecutionGatewayResult> ExecuteAsync(
        ToolExecutionGatewayRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var descriptor = request.EnabledTools.SingleOrDefault(tool =>
            string.Equals(tool.Name, request.ToolCall.Name, StringComparison.Ordinal));
        if (descriptor is null)
        {
            return Denied(request.ToolCall, "tool_not_enabled");
        }

        return request.ToolCall.Name switch
        {
            MvpToolKeys.CheckGoogleCalendarAvailability => await ExecuteAvailabilityAsync(request, descriptor, cancellationToken),
            MvpToolKeys.CreateGoogleCalendarReservation => await ExecuteReservationAsync(request, descriptor, cancellationToken),
            _ => Denied(request.ToolCall, "tool_not_supported"),
        };
    }

    private async Task<ToolExecutionGatewayResult> ExecuteAvailabilityAsync(
        ToolExecutionGatewayRequest request,
        AgentToolDescriptor descriptor,
        CancellationToken cancellationToken)
    {
        if (!TryDeserialize<CheckAvailabilityRequest>(request.ToolCall.Arguments, out var arguments))
        {
            return await PersistDeniedAsync(request, descriptor, "malformed_arguments", cancellationToken);
        }

        var execution = await googleCalendarToolExecutor.CheckAvailabilityAsync(
            request.CompanyId,
            request.ConversationId,
            descriptor.CompanyToolId,
            request.TriggerMessageId,
            arguments,
            CreateIdempotencyKey(request),
            cancellationToken);

        return await ToGatewayResultAsync(request.ToolCall, execution, cancellationToken);
    }

    private async Task<ToolExecutionGatewayResult> ExecuteReservationAsync(
        ToolExecutionGatewayRequest request,
        AgentToolDescriptor descriptor,
        CancellationToken cancellationToken)
    {
        if (!TryDeserialize<CreateCalendarEventRequest>(request.ToolCall.Arguments, out var arguments))
        {
            return await PersistDeniedAsync(request, descriptor, "malformed_arguments", cancellationToken);
        }

        var execution = await googleCalendarToolExecutor.CreateReservationAsync(
            request.CompanyId,
            request.ConversationId,
            descriptor.CompanyToolId,
            request.TriggerMessageId,
            arguments,
            CreateIdempotencyKey(request),
            cancellationToken);

        return await ToGatewayResultAsync(request.ToolCall, execution, cancellationToken);
    }

    private async Task<ToolExecutionGatewayResult> PersistDeniedAsync(
        ToolExecutionGatewayRequest request,
        AgentToolDescriptor descriptor,
        string failureReason,
        CancellationToken cancellationToken)
    {
        var idempotencyKey = CreateIdempotencyKey(request);
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

    private async Task<ToolExecutionGatewayResult> ToGatewayResultAsync(
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
                .SingleOrDefaultAsync(cancellationToken);
            if (resultMessage is not null && resultMessage.MessageText != content)
            {
                resultMessage.MessageText = content;
                await dbContext.SaveChangesAsync(cancellationToken);
            }
        }

        return new ToolExecutionGatewayResult(toolCall.Id, toolCall.Name, content);
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

    private static bool TryDeserialize<TRequest>(JsonElement arguments, out TRequest request)
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

    private static string CreateIdempotencyKey(ToolExecutionGatewayRequest request)
    {
        var canonicalArguments = request.ToolCall.Arguments.GetRawText();
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(canonicalArguments));
        var hash = Convert.ToHexString(hashBytes);
        return $"{request.ConversationId:N}:{request.InboundMessageId:N}:{request.ToolCall.Name}:{hash[..16]}";
    }
}
