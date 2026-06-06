using CeoAgent.Application.Abstractions.AI;
using CeoAgent.Shared.AI;
using CeoAgent.Infrastructure.Implementation.AITools.Execution;
using CeoAgent.Shared.AITools;
using CeoAgent.Shared.Constants;
using CeoAgent.Infrastructure.Entities.JsonDocuments;

namespace CeoAgent.Infrastructure.Implementation.AITools.GoogleCalendar;

public sealed class CreateGoogleCalendarReservationExecutor(
    GoogleCalendarToolExecutor executor,
    ToolExecutionGatewayHelper helper) : IToolExecutor
{
    public string ToolKey => MvpToolKeys.CreateGoogleCalendarReservation;

    public async Task<ToolExecutionGatewayResult> ExecuteAsync(
        ToolExecutionGatewayRequest request,
        AgentToolDescriptor descriptor,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        if (!ToolExecutionGatewayHelper.TryDeserialize<CreateCalendarEventRequest>(request.ToolCall.Arguments, out var arguments)
            || !ToolExecutionGatewayHelper.IsValid(arguments))
        {
            return await helper.PersistDeniedAsync(request, descriptor, "malformed_arguments", idempotencyKey, cancellationToken);
        }

        var execution = await executor.CreateReservationAsync(
            request.CompanyId,
            request.ConversationId,
            descriptor.CompanyToolId,
            request.TriggerMessageId,
            arguments,
            idempotencyKey,
            cancellationToken);

        return await helper.ToGatewayResultAsync(request.ToolCall, execution, cancellationToken);
    }
}
