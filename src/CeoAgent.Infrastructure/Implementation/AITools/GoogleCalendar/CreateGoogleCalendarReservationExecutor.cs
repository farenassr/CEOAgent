using CeoAgent.Application.Abstractions.AITools;
using CeoAgent.Infrastructure.Entities;
using CeoAgent.Infrastructure.Entities.JsonDocuments;
using CeoAgent.Infrastructure.Implementation.AITools.Execution;
using CeoAgent.Shared.Constants;

namespace CeoAgent.Infrastructure.Implementation.AITools.GoogleCalendar;

public sealed class CreateGoogleCalendarReservationTool(
    GoogleCalendarToolExecutor executor) : AgentTool<CreateCalendarEventRequest>
{
    public override string ToolKey => MvpToolKeys.CreateGoogleCalendarReservation;

    public override bool IsMutating => true;

    public override string Description => "Create a Google Calendar reservation after explicit customer confirmation.";

    public override bool Validate(CreateCalendarEventRequest request)
    {
        return request.Start != default
            && request.End != default
            && request.End > request.Start
            && !string.IsNullOrWhiteSpace(request.Summary)
            && !string.IsNullOrWhiteSpace(request.CustomerName);
    }

    protected override async Task<ToolExecution> ExecuteToolAsync(
        ToolExecutionContext context,
        CreateCalendarEventRequest request,
        CancellationToken cancellationToken)
    {
        return await executor.CreateReservationAsync(context, request, cancellationToken);
    }
}
