using CeoAgent.Application.Abstractions.AITools;
using CeoAgent.Infrastructure.Entities;
using CeoAgent.Infrastructure.Entities.JsonDocuments;
using CeoAgent.Infrastructure.Implementation.AITools.Execution;
using CeoAgent.Shared.Constants;

namespace CeoAgent.Infrastructure.Implementation.AITools.GoogleCalendar;

public sealed class FindGoogleCalendarReservationsTool(
    GoogleCalendarToolExecutor executor) : AgentTool<FindGoogleCalendarReservationsRequest>
{
    public override string ToolKey => MvpToolKeys.FindGoogleCalendarReservations;

    public override bool IsMutating => false;

    public override string Description => "Find reservations for the current customer.";

    public override bool Validate(FindGoogleCalendarReservationsRequest request)
    {
        return request.Status is null
            || string.Equals(request.Status, "active", StringComparison.OrdinalIgnoreCase)
            || string.Equals(request.Status, "cancelled", StringComparison.OrdinalIgnoreCase);
    }

    protected override async Task<ToolExecution> ExecuteToolAsync(
        ToolExecutionContext context,
        FindGoogleCalendarReservationsRequest request,
        CancellationToken cancellationToken)
    {
        return await executor.FindReservationsAsync(context, request, cancellationToken);
    }
}
