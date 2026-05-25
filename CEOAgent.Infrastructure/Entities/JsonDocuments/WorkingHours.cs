namespace CeoAgent.Infrastructure.Entities.JsonDocuments;

public sealed class WorkingHours
{
    public Dictionary<DayOfWeek, List<TimeSlot>> Schedule { get; set; } = [];

    public List<SpecialDay> Holidays { get; set; } = [];
}

public sealed class SpecialDay
{
    public DateOnly Date { get; set; }

    public bool IsClosed { get; set; }

    public List<TimeSlot> TimeSlots { get; set; } = [];

    public string? Reason { get; set; }
}

public sealed class TimeSlot
{
    public TimeOnly Start { get; set; }

    public TimeOnly End { get; set; }
}
