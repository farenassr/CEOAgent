using CeoAgent.Infrastructure.Entities.JsonDocuments;
using CeoAgent.Infrastructure.Implementation.AITools.GoogleCalendar;
using Shouldly;

namespace CeoAgent.Worker.Tests.Jobs;

public sealed class GoogleCalendarSchedulingPolicyTests
{
    [Test]
    public void BuildAlternativeStarts_ReturnsThreeBeforeAndThreeAfterInsideSearchWindow()
    {
        var workingHours = WorkingHoursForThursday(new TimeOnly(9, 0), new TimeOnly(22, 0));
        var requestedStart = new DateTimeOffset(2026, 5, 28, 14, 0, 0, TimeSpan.FromHours(-5));

        var alternatives = GoogleCalendarSchedulingPolicy.BuildAlternativeStarts(
            workingHours,
            new DateOnly(2026, 5, 28),
            requestedStart);

        alternatives.ShouldBe(
        [
            new DateTimeOffset(2026, 5, 28, 13, 30, 0, TimeSpan.FromHours(-5)),
            new DateTimeOffset(2026, 5, 28, 13, 0, 0, TimeSpan.FromHours(-5)),
            new DateTimeOffset(2026, 5, 28, 12, 30, 0, TimeSpan.FromHours(-5)),
            new DateTimeOffset(2026, 5, 28, 14, 30, 0, TimeSpan.FromHours(-5)),
            new DateTimeOffset(2026, 5, 28, 15, 0, 0, TimeSpan.FromHours(-5)),
            new DateTimeOffset(2026, 5, 28, 15, 30, 0, TimeSpan.FromHours(-5)),
        ]);
    }

    [Test]
    public void BuildAlternativeStarts_FillsFromOtherSideWhenOneSideHasTooFewSlots()
    {
        var workingHours = WorkingHoursForThursday(new TimeOnly(13, 30), new TimeOnly(19, 0));
        var requestedStart = new DateTimeOffset(2026, 5, 28, 14, 0, 0, TimeSpan.FromHours(-5));

        var alternatives = GoogleCalendarSchedulingPolicy.BuildAlternativeStarts(
            workingHours,
            new DateOnly(2026, 5, 28),
            requestedStart);

        alternatives.ShouldBe(
        [
            new DateTimeOffset(2026, 5, 28, 13, 30, 0, TimeSpan.FromHours(-5)),
            new DateTimeOffset(2026, 5, 28, 14, 30, 0, TimeSpan.FromHours(-5)),
            new DateTimeOffset(2026, 5, 28, 15, 0, 0, TimeSpan.FromHours(-5)),
            new DateTimeOffset(2026, 5, 28, 15, 30, 0, TimeSpan.FromHours(-5)),
            new DateTimeOffset(2026, 5, 28, 16, 0, 0, TimeSpan.FromHours(-5)),
            new DateTimeOffset(2026, 5, 28, 16, 30, 0, TimeSpan.FromHours(-5)),
        ]);
    }

    private static WorkingHours WorkingHoursForThursday(TimeOnly start, TimeOnly end)
    {
        return new WorkingHours
        {
            Schedule = new WeeklySchedule
            {
                Thursday =
                [
                    new TimeSlot
                    {
                        Start = start,
                        End = end,
                    },
                ],
            },
        };
    }
}
