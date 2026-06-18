using CeoAgent.Worker.Jobs;
using Shouldly;
using InfrastructureSpecialDay = CeoAgent.Infrastructure.Entities.JsonDocuments.SpecialDay;
using InfrastructureTimeSlot = CeoAgent.Infrastructure.Entities.JsonDocuments.TimeSlot;
using InfrastructureWeeklySchedule = CeoAgent.Infrastructure.Entities.JsonDocuments.WeeklySchedule;
using InfrastructureWorkingHours = CeoAgent.Infrastructure.Entities.JsonDocuments.WorkingHours;

namespace CeoAgent.Worker.Tests.Jobs;

public sealed class WorkingHoursSharedAdapterTests
{
    [Test]
    public void ToShared_PreservesWeeklyScheduleAndHolidays()
    {
        var source = new InfrastructureWorkingHours
        {
            Schedule = new InfrastructureWeeklySchedule
            {
                Monday =
                [
                    new InfrastructureTimeSlot { Start = new TimeOnly(9, 0), End = new TimeOnly(12, 0) },
                    new InfrastructureTimeSlot { Start = new TimeOnly(13, 0), End = new TimeOnly(18, 0) },
                ],
                Sunday =
                [
                    new InfrastructureTimeSlot { Start = new TimeOnly(10, 0), End = new TimeOnly(14, 0) },
                ],
            },
            Holidays =
            [
                new InfrastructureSpecialDay
                {
                    Date = new DateOnly(2026, 12, 24),
                    IsClosed = false,
                    Reason = "Short day",
                    TimeSlots =
                    [
                        new InfrastructureTimeSlot { Start = new TimeOnly(9, 30), End = new TimeOnly(13, 30) },
                    ],
                },
            ],
        };

        var result = WorkingHoursSharedAdapter.ToShared(source);

        result.ShouldNotBeNull();
        result.Schedule.Monday.Select(slot => (slot.Start, slot.End)).ShouldBe([
            (new TimeOnly(9, 0), new TimeOnly(12, 0)),
            (new TimeOnly(13, 0), new TimeOnly(18, 0)),
        ]);
        result.Schedule.Sunday.Select(slot => (slot.Start, slot.End)).ShouldBe([
            (new TimeOnly(10, 0), new TimeOnly(14, 0)),
        ]);
        result.Schedule.Tuesday.ShouldBeEmpty();
        var holiday = result.Holidays.Single();
        holiday.Date.ShouldBe(new DateOnly(2026, 12, 24));
        holiday.IsClosed.ShouldBeFalse();
        holiday.Reason.ShouldBe("Short day");
        holiday.TimeSlots.Select(slot => (slot.Start, slot.End)).ShouldBe([
            (new TimeOnly(9, 30), new TimeOnly(13, 30)),
        ]);
    }

    [Test]
    public void ToShared_WhenWorkingHoursMissing_ReturnsNull()
    {
        WorkingHoursSharedAdapter.ToShared(null).ShouldBeNull();
    }
}
