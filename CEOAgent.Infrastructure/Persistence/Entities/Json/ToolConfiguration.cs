using System.Text.Json.Serialization;

namespace CEOAgent.Infrastructure.Persistence.Entities.Json;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "toolKey")]
[JsonDerivedType(typeof(CheckAvailabilityConfig), "check_availability")]
[JsonDerivedType(typeof(RequestHumanHandoffConfig), "request_human_handoff")]
[JsonDerivedType(typeof(GoogleCalendarConfig), "google_calendar")]
public abstract class ToolConfiguration;

public sealed class CheckAvailabilityConfig : ToolConfiguration
{
    public int MaxPartySize { get; set; }

    public int MinPartySize { get; set; }

    public int SlotMinutes { get; set; }

    public int AdvanceBookingDays { get; set; }
}

public sealed class RequestHumanHandoffConfig : ToolConfiguration
{
    public string? EscalationChannel { get; set; }

    public List<string> NotifyUsers { get; set; } = [];

    public int TimeoutMinutes { get; set; }
}

public sealed class GoogleCalendarConfig : ToolConfiguration
{
    public required string CalendarId { get; set; }

    public required string TimeZoneId { get; set; }

    public int BufferMinutes { get; set; }
}
