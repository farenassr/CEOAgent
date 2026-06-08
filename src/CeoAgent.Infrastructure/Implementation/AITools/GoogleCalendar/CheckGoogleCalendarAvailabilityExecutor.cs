using CeoAgent.Application.Abstractions.AITools;
using CeoAgent.Infrastructure.Entities;
using CeoAgent.Infrastructure.Entities.JsonDocuments;
using CeoAgent.Infrastructure.Implementation.AITools.Execution;
using CeoAgent.Shared.Constants;

namespace CeoAgent.Infrastructure.Implementation.AITools.GoogleCalendar;

public sealed class CheckGoogleCalendarAvailabilityTool(
    GoogleCalendarToolExecutor executor) : AgentTool<CheckAvailabilityRequest>
{
    public override string ToolKey => MvpToolKeys.CheckGoogleCalendarAvailability;

    public override bool IsMutating => false;

    public override string Description => "Check Google Calendar availability before offering or confirming reservation times.";

    public override bool Validate(CheckAvailabilityRequest request)
    {
        return request.Date != default
            && request.PartySize > 0;
    }

    protected override async Task<ToolExecution> ExecuteToolAsync(
        ToolExecutionContext context,
        CheckAvailabilityRequest request,
        CancellationToken cancellationToken)
    {
        return await executor.CheckAvailabilityAsync(context, request, cancellationToken);
    }
}
