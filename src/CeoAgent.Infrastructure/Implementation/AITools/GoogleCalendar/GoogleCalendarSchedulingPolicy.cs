using CeoAgent.Infrastructure.Entities.JsonDocuments;

namespace CeoAgent.Infrastructure.Implementation.AITools.GoogleCalendar;

/// <summary>
/// Defines shared scheduling rules for Google Calendar tools, including local time conversion, working-hour checks, advance booking limits, and alternative slot generation.
/// </summary>
public static class GoogleCalendarSchedulingPolicy
{
    public const int DefaultReservationMinutes = GoogleCalendarSchedulingDefaults.ReservationMinutes;

    public const int DefaultSlotMinutes = GoogleCalendarSchedulingDefaults.SlotMinutes;

    public const int AlternativeSearchWindowHours = 3;

    public const int MaxAlternativeStarts = 6;

    private const int PreferredAlternativeStartsPerSide = 3;

    /// <summary>
    /// Combines a company-local date and time with the configured time zone offset for that local instant.
    /// </summary>
    public static DateTimeOffset ToCompanyLocalOffset(DateOnly date, TimeOnly time, string timeZoneId)
    {
        var localDateTime = date.ToDateTime(time);
        var timeZone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        var offset = timeZone.GetUtcOffset(localDateTime);
        return new DateTimeOffset(localDateTime, offset);
    }

    /// <summary>
    /// Returns the first opening time configured for the requested date, including any holiday override.
    /// </summary>
    public static TimeOnly? FirstWorkingTime(WorkingHours? workingHours, DateOnly date)
    {
        return SlotsForDate(workingHours, date)
            .OrderBy(slot => slot.Start)
            .Select(slot => (TimeOnly?)slot.Start)
            .FirstOrDefault();
    }

    /// <summary>
    /// Determines whether the requested interval, expanded by the optional buffer, fits inside a configured working-hours slot.
    /// </summary>
    public static bool IsWithinWorkingHours(
        WorkingHours? workingHours,
        DateTimeOffset start,
        DateTimeOffset end,
        int bufferMinutes = 0)
    {
        var bufferedStart = start.AddMinutes(-bufferMinutes);
        var bufferedEnd = end.AddMinutes(bufferMinutes);
        var date = DateOnly.FromDateTime(bufferedStart.DateTime);
        var startTime = TimeOnly.FromDateTime(bufferedStart.DateTime);
        var endTime = TimeOnly.FromDateTime(bufferedEnd.DateTime);

        return SlotsForDate(workingHours, date)
            .Any(slot => startTime >= slot.Start && endTime <= slot.End);
    }

    /// <summary>
    /// Determines whether a requested booking date is today or within the company's configured future booking window.
    /// </summary>
    public static bool IsWithinAdvanceWindow(
        DateOnly date,
        string timeZoneId,
        DateTimeOffset now,
        int advanceBookingDays)
    {
        var timeZone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        var localToday = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, timeZone).DateTime);
        return date >= localToday && date <= localToday.AddDays(advanceBookingDays);
    }

    /// <summary>
    /// Builds nearby candidate reservation start times inside the availability search window while respecting slots, duration, and buffer.
    /// </summary>
    public static DateTimeOffset[] BuildAlternativeStarts(
        WorkingHours? workingHours,
        DateOnly date,
        DateTimeOffset requestedStart,
        int slotMinutes = DefaultSlotMinutes,
        int reservationMinutes = DefaultReservationMinutes,
        int bufferMinutes = 0)
    {
        var searchWindowStart = requestedStart.AddHours(-AlternativeSearchWindowHours);
        var searchWindowEnd = requestedStart.AddHours(AlternativeSearchWindowHours);
        var alternatives = new List<DateTimeOffset>();
        foreach (var slot in SlotsForDate(workingHours, date).OrderBy(slot => slot.Start))
        {
            var cursor = new DateTimeOffset(date.ToDateTime(slot.Start), requestedStart.Offset)
                .AddMinutes(bufferMinutes);
            var latestStart = new DateTimeOffset(date.ToDateTime(slot.End), requestedStart.Offset)
                .AddMinutes(-reservationMinutes)
                .AddMinutes(-bufferMinutes);
            while (cursor <= latestStart)
            {
                if (cursor != requestedStart
                    && cursor >= searchWindowStart
                    && cursor <= searchWindowEnd)
                {
                    alternatives.Add(cursor);
                }

                cursor = cursor.AddMinutes(slotMinutes);
            }
        }

        var selected = alternatives
            .Where(value => value < requestedStart)
            .OrderByDescending(value => value)
            .Take(PreferredAlternativeStartsPerSide)
            .Concat(alternatives
                .Where(value => value > requestedStart)
                .OrderBy(value => value)
                .Take(PreferredAlternativeStartsPerSide))
            .ToList();

        if (selected.Count < MaxAlternativeStarts)
        {
            selected.AddRange(alternatives
                .Except(selected)
                .OrderBy(value => Math.Abs((value - requestedStart).TotalMinutes))
                .ThenBy(value => value)
                .Take(MaxAlternativeStarts - selected.Count));
        }

        return selected
            .Take(MaxAlternativeStarts)
            .ToArray();
    }

    /// <summary>
    /// Resolves the working-hour slots that apply to a date, preferring holiday overrides before the weekly schedule.
    /// </summary>
    private static List<TimeSlot> SlotsForDate(WorkingHours? workingHours, DateOnly date)
    {
        if (workingHours is null)
        {
            return [];
        }

        var specialDay = workingHours.Holidays.FirstOrDefault(day => day.Date == date);
        if (specialDay is { IsClosed: true })
        {
            return [];
        }

        if (specialDay is not null)
        {
            return specialDay.TimeSlots;
        }

        return date.DayOfWeek switch
        {
            DayOfWeek.Monday => workingHours.Schedule.Monday,
            DayOfWeek.Tuesday => workingHours.Schedule.Tuesday,
            DayOfWeek.Wednesday => workingHours.Schedule.Wednesday,
            DayOfWeek.Thursday => workingHours.Schedule.Thursday,
            DayOfWeek.Friday => workingHours.Schedule.Friday,
            DayOfWeek.Saturday => workingHours.Schedule.Saturday,
            DayOfWeek.Sunday => workingHours.Schedule.Sunday,
            _ => [],
        };
    }
}
