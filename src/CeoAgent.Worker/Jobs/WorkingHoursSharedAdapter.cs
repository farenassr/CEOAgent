using InfrastructureWorkingHours = CeoAgent.Infrastructure.Entities.JsonDocuments.WorkingHours;
using InfrastructureTimeSlot = CeoAgent.Infrastructure.Entities.JsonDocuments.TimeSlot;
using SharedSpecialDay = CeoAgent.Shared.JsonDocuments.SpecialDay;
using SharedTimeSlot = CeoAgent.Shared.JsonDocuments.TimeSlot;
using SharedWeeklySchedule = CeoAgent.Shared.JsonDocuments.WeeklySchedule;
using SharedWorkingHours = CeoAgent.Shared.JsonDocuments.WorkingHours;

namespace CeoAgent.Worker.Jobs;

public static class WorkingHoursSharedAdapter
{
    public static SharedWorkingHours? ToShared(InfrastructureWorkingHours? workingHours)
    {
        if (workingHours is null)
        {
            return null;
        }

        return new SharedWorkingHours
        {
            Schedule = new SharedWeeklySchedule
            {
                Monday = ConvertSlots(workingHours.Schedule.Monday),
                Tuesday = ConvertSlots(workingHours.Schedule.Tuesday),
                Wednesday = ConvertSlots(workingHours.Schedule.Wednesday),
                Thursday = ConvertSlots(workingHours.Schedule.Thursday),
                Friday = ConvertSlots(workingHours.Schedule.Friday),
                Saturday = ConvertSlots(workingHours.Schedule.Saturday),
                Sunday = ConvertSlots(workingHours.Schedule.Sunday),
            },
            Holidays = workingHours.Holidays
                .ConvertAll(holiday => new SharedSpecialDay
                {
                    Date = holiday.Date,
                    IsClosed = holiday.IsClosed,
                    Reason = holiday.Reason,
                    TimeSlots = ConvertSlots(holiday.TimeSlots),
                }),
        };
    }

    private static List<SharedTimeSlot> ConvertSlots(IEnumerable<InfrastructureTimeSlot> slots)
    {
        return slots
            .Select(slot => new SharedTimeSlot
            {
                Start = slot.Start,
                End = slot.End,
            })
            .ToList();
    }
}
