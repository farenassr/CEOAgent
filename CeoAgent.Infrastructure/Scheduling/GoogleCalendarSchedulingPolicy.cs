using CeoAgent.Infrastructure.Entities.JsonDocuments;

namespace CeoAgent.Infrastructure.Scheduling;

public static class GoogleCalendarSchedulingPolicy
{
    public const int DefaultReservationMinutes = GoogleCalendarSchedulingDefaults.ReservationMinutes;

    public const int DefaultSlotMinutes = GoogleCalendarSchedulingDefaults.SlotMinutes;

    public static DateTimeOffset ToCompanyLocalOffset(DateOnly date, TimeOnly time, string timeZoneId)
    {
        var localDateTime = date.ToDateTime(time);
        var timeZone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        var offset = timeZone.GetUtcOffset(localDateTime);
        return new DateTimeOffset(localDateTime, offset);
    }

    public static TimeOnly? FirstWorkingTime(WorkingHours? workingHours, DateOnly date)
    {
        return SlotsForDate(workingHours, date)
            .OrderBy(slot => slot.Start)
            .Select(slot => (TimeOnly?)slot.Start)
            .FirstOrDefault();
    }

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

    public static DateTimeOffset[] BuildAlternativeStarts(
        WorkingHours? workingHours,
        DateOnly date,
        DateTimeOffset requestedStart,
        int slotMinutes = DefaultSlotMinutes,
        int reservationMinutes = DefaultReservationMinutes,
        int bufferMinutes = 0)
    {
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
                if (cursor != requestedStart)
                {
                    alternatives.Add(cursor);
                }

                cursor = cursor.AddMinutes(slotMinutes);
            }
        }

        return alternatives
            .OrderBy(value => Math.Abs((value - requestedStart).TotalMinutes))
            .ThenBy(value => value)
            .ToArray();
    }

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
