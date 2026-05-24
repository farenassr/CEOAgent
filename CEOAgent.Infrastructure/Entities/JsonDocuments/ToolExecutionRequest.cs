using System.Text.Json.Serialization;

namespace CEOAgent.Infrastructure.Entities.JsonDocuments;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "toolKey")]
[JsonDerivedType(typeof(CheckAvailabilityRequest), "check_availability")]
[JsonDerivedType(typeof(RequestHumanHandoffRequest), "request_human_handoff")]
[JsonDerivedType(typeof(CreateCalendarEventRequest), "create_calendar_event")]
public abstract class ToolExecutionRequest;

public sealed class CheckAvailabilityRequest : ToolExecutionRequest
{
    public DateOnly Date { get; set; }

    public int PartySize { get; set; }

    public TimeOnly? PreferredTime { get; set; }
}

public sealed class RequestHumanHandoffRequest : ToolExecutionRequest
{
    public required string Reason { get; set; }

    public string? Notes { get; set; }
}

public sealed class CreateCalendarEventRequest : ToolExecutionRequest
{
    public DateTimeOffset Start { get; set; }

    public DateTimeOffset End { get; set; }

    public required string Summary { get; set; }
}
