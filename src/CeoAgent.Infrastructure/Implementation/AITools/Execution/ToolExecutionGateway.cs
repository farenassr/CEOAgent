using CeoAgent.Application.Abstractions.AITools;
using System.Diagnostics;
using CeoAgent.Application;
using CeoAgent.Shared.AI;
using CeoAgent.Shared.AITools;

namespace CeoAgent.Infrastructure.Implementation.AITools.Execution;

public sealed class ToolExecutionGateway
{
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
        activity?.SetTag("organization.id", request.OrganizationId);
        activity?.SetTag("conversation.id", request.ConversationId);
        activity?.SetTag("tool.key", request.ToolCall.Name);
        activity?.SetTag("tool.side_effects_enabled", request.SideEffectsEnabled);
        activity?.SetTag(CeoAgentTelemetry.Langfuse.ObservationType, "tool");
        activity?.SetTag(CeoAgentTelemetry.Langfuse.MetadataOrganizationId, request.OrganizationId.ToString());
        activity?.SetTag(CeoAgentTelemetry.Langfuse.MetadataConversationId, request.ConversationId.ToString());
        activity?.SetTag(CeoAgentTelemetry.Langfuse.MetadataToolKey, request.ToolCall.Name);
        var stopwatch = Stopwatch.StartNew();

        var descriptor = request.EnabledTools.SingleOrDefault(tool =>
            string.Equals(tool.Name, request.ToolCall.Name, StringComparison.Ordinal));
        if (descriptor is null)
        {
            var deniedIdempotencyKey = ToolExecutionGatewayHelper.CreateIdempotencyKey(request);
            var denied = await _helper.PersistToolNotEnabledDeniedAsync(
                request,
                deniedIdempotencyKey,
                cancellationToken);
            stopwatch.Stop();
            CeoAgentTelemetry.ToolExecutionDuration.Record(stopwatch.ElapsedMilliseconds);
            CeoAgentTelemetry.ToolExecutionFailures.Add(1);
            activity?.SetTag("tool.status", "denied");
            activity?.SetTag("tool.failure_reason", "tool_not_enabled");
            activity?.SetTag(CeoAgentTelemetry.Langfuse.MetadataToolStatus, "denied");
            activity?.SetTag(CeoAgentTelemetry.Langfuse.MetadataToolFailureReason, "tool_not_enabled");
            activity?.SetStatus(ActivityStatusCode.Ok);
            return denied;
        }

        activity?.SetTag("tool.mutating", descriptor.IsMutating);
        activity?.SetTag(CeoAgentTelemetry.Langfuse.MetadataToolMutating, descriptor.IsMutating);
        var idempotencyKey = ToolExecutionGatewayHelper.CreateIdempotencyKey(request);

        if (!request.SideEffectsEnabled && descriptor.IsMutating)
        {
            var denied = await _helper.PersistDeniedAsync(request, descriptor, "side_effects_disabled", idempotencyKey, cancellationToken);
            stopwatch.Stop();
            CeoAgentTelemetry.ToolExecutionDuration.Record(stopwatch.ElapsedMilliseconds);
            CeoAgentTelemetry.ToolExecutionFailures.Add(1);
            activity?.SetTag("tool.status", "denied");
            activity?.SetTag("tool.failure_reason", "side_effects_disabled");
            activity?.SetTag(CeoAgentTelemetry.Langfuse.MetadataToolStatus, "denied");
            activity?.SetTag(CeoAgentTelemetry.Langfuse.MetadataToolFailureReason, "side_effects_disabled");
            activity?.SetStatus(ActivityStatusCode.Ok);
            return denied;
        }

        try
        {
            var result = await _invoker.ExecuteAsync(request, descriptor, idempotencyKey, cancellationToken);
            stopwatch.Stop();
            CeoAgentTelemetry.ToolExecutionDuration.Record(stopwatch.ElapsedMilliseconds);
            activity?.SetTag("tool.status", "completed");
            activity?.SetTag(CeoAgentTelemetry.Langfuse.MetadataToolStatus, "completed");
            activity?.SetStatus(ActivityStatusCode.Ok);
            return result;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            stopwatch.Stop();
            CeoAgentTelemetry.ToolExecutionDuration.Record(stopwatch.ElapsedMilliseconds);
            CeoAgentTelemetry.ToolExecutionFailures.Add(1);
            activity?.SetTag("tool.status", "failed");
            activity?.SetTag(CeoAgentTelemetry.Langfuse.MetadataToolStatus, "failed");
            activity?.SetTag(CeoAgentTelemetry.Langfuse.MetadataToolFailureReason, exception.GetType().Name);
            activity?.SetStatus(ActivityStatusCode.Error, exception.GetType().Name);
            throw;
        }
    }

}
