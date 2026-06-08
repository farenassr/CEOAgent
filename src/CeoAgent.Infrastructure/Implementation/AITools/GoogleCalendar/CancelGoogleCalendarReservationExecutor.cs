using CeoAgent.Application.Abstractions.AITools;
using CeoAgent.Infrastructure.Entities;
using CeoAgent.Infrastructure.Entities.JsonDocuments;
using CeoAgent.Infrastructure.Implementation.AITools.Execution;
using CeoAgent.Shared.Constants;

namespace CeoAgent.Infrastructure.Implementation.AITools.GoogleCalendar;

public sealed class CancelGoogleCalendarReservationTool(
    GoogleCalendarToolExecutor executor) : AgentTool<CancelGoogleCalendarReservationRequest>
{
    public override string ToolKey => MvpToolKeys.CancelGoogleCalendarReservation;

    public override bool IsMutating => true;

    public override string Description => "Cancel an existing Google Calendar reservation owned by the current customer.";

    public override bool Validate(CancelGoogleCalendarReservationRequest request)
    {
        return !string.IsNullOrWhiteSpace(request.ReservationId);
    }

    protected override async Task<ToolExecution> ExecuteToolAsync(
        ToolExecutionContext context,
        CancelGoogleCalendarReservationRequest request,
        CancellationToken cancellationToken)
    {
        return await executor.CancelReservationAsync(context, request, cancellationToken);
    }
}
