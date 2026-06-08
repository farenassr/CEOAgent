using CeoAgent.Application.Abstractions.AITools;
using System.Text.Json;
using System.Diagnostics;
using CeoAgent.Application;
using CeoAgent.Application.Abstractions.AI;
using CeoAgent.Shared.AI;
using CeoAgent.Shared.AITools;

namespace CeoAgent.Infrastructure.Implementation.AITools.Execution;

public sealed class ToolExecutionGateway
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly IAgentToolInvoker _invoker;
    private readonly ToolExecutionGatewayHelper _helper;

    public ToolExecutionGateway(
        IAgentToolInvoker invoker,
        ToolExecutionGatewayHelper helper)
    {
        _invoker = invoker;
        _helper = helper;
    }

    public async Task<ToolExecutionGatewayResult> ExecuteAsync(
        ToolExecutionGatewayRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        using var activity = CeoAgentTelemetry.ActivitySource.StartActivity("tool.execution");
        activity?.SetTag("company.id", request.CompanyId);
        activity?.SetTag("conversation.id", request.ConversationId);
        activity?.SetTag("tool.key", request.ToolCall.Name);
        activity?.SetTag("tool.side_effects_enabled", request.SideEffectsEnabled);
        var stopwatch = Stopwatch.StartNew();

        var descriptor = request.EnabledTools.SingleOrDefault(tool =>
            string.Equals(tool.Name, request.ToolCall.Name, StringComparison.Ordinal));
        if (descriptor is null)
        {
            stopwatch.Stop();
            CeoAgentTelemetry.ToolExecutionDuration.Record(stopwatch.ElapsedMilliseconds);
            CeoAgentTelemetry.ToolExecutionFailures.Add(1);
            activity?.SetTag("tool.status", "denied");
            activity?.SetTag("tool.failure_reason", "tool_not_enabled");
            activity?.SetStatus(ActivityStatusCode.Ok);
            return Denied(request.ToolCall, "tool_not_enabled");
        }

        activity?.SetTag("tool.mutating", descriptor.IsMutating);
        var idempotencyKey = ToolExecutionGatewayHelper.CreateIdempotencyKey(request);

        if (!request.SideEffectsEnabled && descriptor.IsMutating)
        {
            var denied = await _helper.PersistDeniedAsync(request, descriptor, "side_effects_disabled", idempotencyKey, cancellationToken);
            stopwatch.Stop();
            CeoAgentTelemetry.ToolExecutionDuration.Record(stopwatch.ElapsedMilliseconds);
            CeoAgentTelemetry.ToolExecutionFailures.Add(1);
            activity?.SetTag("tool.status", "denied");
            activity?.SetTag("tool.failure_reason", "side_effects_disabled");
            activity?.SetStatus(ActivityStatusCode.Ok);
            return denied;
        }

        try
        {
            var result = await _invoker.ExecuteAsync(request, descriptor, idempotencyKey, cancellationToken);
            stopwatch.Stop();
            CeoAgentTelemetry.ToolExecutionDuration.Record(stopwatch.ElapsedMilliseconds);
            activity?.SetTag("tool.status", "completed");
            activity?.SetStatus(ActivityStatusCode.Ok);
            return result;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            stopwatch.Stop();
            CeoAgentTelemetry.ToolExecutionDuration.Record(stopwatch.ElapsedMilliseconds);
            CeoAgentTelemetry.ToolExecutionFailures.Add(1);
            activity?.SetTag("tool.status", "failed");
            activity?.SetStatus(ActivityStatusCode.Error, exception.GetType().Name);
            throw;
        }
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
}
