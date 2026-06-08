using CeoAgent.Application.Abstractions.AITools;
using CeoAgent.Infrastructure.Entities;
using CeoAgent.Infrastructure.Entities.JsonDocuments;
using CeoAgent.Infrastructure.Implementation.AITools.Execution;
using CeoAgent.Shared.Constants;

namespace CeoAgent.Infrastructure.Implementation.AITools.GoogleCalendar;

public sealed class UpdateGoogleCalendarReservationTool(
    GoogleCalendarToolExecutor executor) : AgentTool<UpdateGoogleCalendarReservationRequest>
{
    public override string ToolKey => MvpToolKeys.UpdateGoogleCalendarReservation;

    public override bool IsMutating => true;

    public override string Description => "Update an existing Google Calendar reservation owned by the current customer.";

    public override bool Validate(UpdateGoogleCalendarReservationRequest request)
    {
        return !string.IsNullOrWhiteSpace(request.ReservationId)
            && request.NewStart != default
            && request.NewEnd != default
            && request.NewEnd > request.NewStart;
    }

    protected override async Task<ToolExecution> ExecuteToolAsync(
        ToolExecutionContext context,
        UpdateGoogleCalendarReservationRequest request,
        CancellationToken cancellationToken)
    {
        return await executor.UpdateReservationAsync(context, request, cancellationToken);
    }
}
