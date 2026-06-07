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

namespace CeoAgent.Infrastructure.Implementation.AITools.Execution;

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
            && !string.IsNullOrWhiteSpace(request.Summary)
            && !string.IsNullOrWhiteSpace(request.CustomerName);
    }

    public static bool IsValid(FindGoogleCalendarReservationsRequest request)
    {
        return request.Status is null
            || string.Equals(request.Status, "active", StringComparison.OrdinalIgnoreCase)
            || string.Equals(request.Status, "cancelled", StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsValid(UpdateGoogleCalendarReservationRequest request)
    {
        return !string.IsNullOrWhiteSpace(request.ReservationId)
            && request.NewStart != default
            && request.NewEnd != default
            && request.NewEnd > request.NewStart;
    }

    public static bool IsValid(CancelGoogleCalendarReservationRequest request)
    {
        return !string.IsNullOrWhiteSpace(request.ReservationId);
    }

    public async Task<ToolExecutionGatewayResult> ExecuteValidatedAsync<TRequest>(
        ToolExecutionGatewayRequest request,
        AgentToolDescriptor descriptor,
        string idempotencyKey,
        Func<TRequest, bool> isValid,
        Func<TRequest, CancellationToken, Task<ToolExecution>> execute,
        CancellationToken cancellationToken)
        where TRequest : class
    {
        if (!TryDeserialize<TRequest>(request.ToolCall.Arguments, out var arguments)
            || !isValid(arguments))
        {
            return await PersistDeniedAsync(request, descriptor, "malformed_arguments", idempotencyKey, cancellationToken);
        }

        var execution = await execute(arguments, cancellationToken);
        return await ToGatewayResultAsync(request.ToolCall, execution, cancellationToken);
    }

    public async Task<ToolExecutionGatewayResult> PersistDeniedAsync(
        ToolExecutionGatewayRequest request,
        AgentToolDescriptor descriptor,
        string failureReason,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        var existing = await dbContext.FindTrackedOrPersistedToolExecutionAsync(
            request.CompanyId,
            idempotencyKey,
            cancellationToken);
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
            var resultMessage = await dbContext.FindTrackedOrPersistedMessageAsync(
                execution.CompanyId,
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
}
