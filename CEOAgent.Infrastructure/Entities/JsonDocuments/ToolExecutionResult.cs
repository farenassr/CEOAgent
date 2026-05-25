using System.Text.Json.Serialization;

namespace CeoAgent.Infrastructure.Entities.JsonDocuments;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "toolKey")]
[JsonDerivedType(typeof(CheckAvailabilityResult), "check_availability")]
[JsonDerivedType(typeof(RequestHumanHandoffResult), "request_human_handoff")]
[JsonDerivedType(typeof(CreateCalendarEventResult), "create_calendar_event")]
public abstract class ToolExecutionResult;

public sealed class CheckAvailabilityResult : ToolExecutionResult
{
    public bool Available { get; set; }

    public List<TimeOnly> AlternativeSlots { get; set; } = [];

    public string? UnavailabilityReason { get; set; }
}

public sealed class RequestHumanHandoffResult : ToolExecutionResult
{
    public bool HandoffRequested { get; set; }

    public string? HandoffTicketId { get; set; }

    public DateTimeOffset? EstimatedPickupAt { get; set; }
}

public sealed class CreateCalendarEventResult : ToolExecutionResult
{
    public required string EventId { get; set; }

    public required string EventUrl { get; set; }
}
