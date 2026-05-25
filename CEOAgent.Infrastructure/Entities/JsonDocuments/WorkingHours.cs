namespace CeoAgent.Infrastructure.Entities.JsonDocuments;

public sealed class WorkingHours
{
    /// <summary>
    /// Weekly recurring availability schedule.
    /// </summary>
    public WeeklySchedule Schedule { get; set; } = new();

    /// <summary>
    /// Date-specific overrides for holidays or exceptional operating days.
    /// </summary>
    public List<SpecialDay> Holidays { get; set; } = [];
}

public sealed class WeeklySchedule
{
    /// <summary>
    /// Monday operating time slots.
    /// </summary>
    public List<TimeSlot> Monday { get; set; } = [];

    /// <summary>
    /// Tuesday operating time slots.
    /// </summary>
    public List<TimeSlot> Tuesday { get; set; } = [];

    /// <summary>
    /// Wednesday operating time slots.
    /// </summary>
    public List<TimeSlot> Wednesday { get; set; } = [];

    /// <summary>
    /// Thursday operating time slots.
    /// </summary>
    public List<TimeSlot> Thursday { get; set; } = [];

    /// <summary>
    /// Friday operating time slots.
    /// </summary>
    public List<TimeSlot> Friday { get; set; } = [];

    /// <summary>
    /// Saturday operating time slots.
    /// </summary>
    public List<TimeSlot> Saturday { get; set; } = [];

    /// <summary>
    /// Sunday operating time slots.
    /// </summary>
    public List<TimeSlot> Sunday { get; set; } = [];
}

public sealed class SpecialDay
{
    /// <summary>
    /// Local calendar date for the special operating day.
    /// </summary>
    public DateOnly Date { get; set; }

    /// <summary>
    /// Whether the company is closed for the entire date.
    /// </summary>
    public bool IsClosed { get; set; }

    /// <summary>
    /// Replacement time slots for the date when it is not fully closed.
    /// </summary>
    public List<TimeSlot> TimeSlots { get; set; } = [];

    /// <summary>
    /// Optional reason for the date-specific override.
    /// </summary>
    public string? Reason { get; set; }
}

public sealed class TimeSlot
{
    /// <summary>
    /// Local start time for the slot.
    /// </summary>
    public TimeOnly Start { get; set; }

    /// <summary>
    /// Local end time for the slot.
    /// </summary>
    public TimeOnly End { get; set; }
}
