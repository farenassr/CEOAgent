using CeoAgent.Application.Abstractions.AI;
using CeoAgent.Infrastructure.Entities.JsonDocuments;
using CeoAgent.Infrastructure.Implementation.AITools.Execution;
using CeoAgent.Shared.AI;
using CeoAgent.Shared.AITools;
using CeoAgent.Shared.Constants;

namespace CeoAgent.Infrastructure.Implementation.AITools.GoogleCalendar;

public sealed class UpdateGoogleCalendarReservationExecutor(
    GoogleCalendarToolExecutor executor,
    ToolExecutionGatewayHelper helper) : IToolExecutor
{
    public string ToolKey => MvpToolKeys.UpdateGoogleCalendarReservation;

    public async Task<ToolExecutionGatewayResult> ExecuteAsync(
        ToolExecutionGatewayRequest request,
        AgentToolDescriptor descriptor,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        return await helper.ExecuteValidatedAsync<UpdateGoogleCalendarReservationRequest>(
            request,
            descriptor,
            idempotencyKey,
            ToolExecutionGatewayHelper.IsValid,
            (arguments, token) => executor.UpdateReservationAsync(
                request.CompanyId,
                request.ConversationId,
                descriptor.CompanyToolId,
                request.TriggerMessageId,
                arguments,
                idempotencyKey,
                token),
            cancellationToken);
    }
}
