using System.Globalization;
using CeoAgent.Shared.JsonDocuments;

namespace CeoAgent.Shared.Prompt;

/// <summary>
/// Formats weekly and date-specific working hours into compact prompt text for the agent.
/// </summary>
public static class WorkingHoursPromptFormatter
{
    /// <summary>
    /// Produces a semicolon-separated schedule summary, including special closed or replacement days.
    /// </summary>
    public static string Format(WorkingHours? workingHours)
    {
        if (workingHours is null)
        {
            return "not configured";
        }

        var segments = new List<string>
        {
            FormatDay("Mon", workingHours.Schedule.Monday),
            FormatDay("Tue", workingHours.Schedule.Tuesday),
            FormatDay("Wed", workingHours.Schedule.Wednesday),
            FormatDay("Thu", workingHours.Schedule.Thursday),
            FormatDay("Fri", workingHours.Schedule.Friday),
            FormatDay("Sat", workingHours.Schedule.Saturday),
            FormatDay("Sun", workingHours.Schedule.Sunday),
        };

        foreach (var specialDay in workingHours.Holidays.OrderBy(holiday => holiday.Date))
        {
            segments.Add(FormatSpecialDay(specialDay));
        }

        return string.Join("; ", segments);
    }

    private static string FormatDay(string label, List<TimeSlot> slots)
    {
        if (slots.Count == 0)
        {
            return string.Create(CultureInfo.InvariantCulture, $"{label} closed");
        }

        var formattedSlots = slots
            .OrderBy(slot => slot.Start)
            .Select(slot => string.Create(CultureInfo.InvariantCulture, $"{slot.Start:HH:mm}-{slot.End:HH:mm}"));

        return string.Create(CultureInfo.InvariantCulture, $"{label} {string.Join(",", formattedSlots)}");
    }

    private static string FormatSpecialDay(SpecialDay specialDay)
    {
        if (specialDay.IsClosed)
        {
            return string.Create(CultureInfo.InvariantCulture, $"Special {specialDay.Date:yyyy-MM-dd} closed");
        }

        var formattedSlots = specialDay.TimeSlots
            .OrderBy(slot => slot.Start)
            .Select(slot => string.Create(CultureInfo.InvariantCulture, $"{slot.Start:HH:mm}-{slot.End:HH:mm}"));

        return string.Create(
            CultureInfo.InvariantCulture,
            $"Special {specialDay.Date:yyyy-MM-dd} {string.Join(",", formattedSlots)}");
    }
}
