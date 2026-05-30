using CeoAgent.Shared.JsonDocuments;
using CeoAgent.Shared.Prompt;
using Shouldly;

namespace CeoAgent.IntegrationTests.Prompt;

public sealed class WorkingHoursPromptFormatterTests
{
    [Test]
    public void Format_CreatesCompactSummaryFromWorkingHours()
    {
        var workingHours = new WorkingHours
        {
            Schedule = new WeeklySchedule
            {
                Monday = [new TimeSlot { Start = new TimeOnly(12, 0), End = new TimeOnly(22, 0) }],
                Tuesday = [new TimeSlot { Start = new TimeOnly(12, 0), End = new TimeOnly(22, 0) }],
                Saturday = [new TimeSlot { Start = new TimeOnly(13, 0), End = new TimeOnly(23, 30) }],
            },
            Holidays =
            [
                new SpecialDay
                {
                    Date = new DateOnly(2026, 5, 29),
                    IsClosed = true,
                    Reason = "maintenance",
                },
            ],
        };

        var summary = WorkingHoursPromptFormatter.Format(workingHours);

        summary.ShouldBe("Mon 12:00-22:00; Tue 12:00-22:00; Wed closed; Thu closed; Fri closed; Sat 13:00-23:30; Sun closed; Special 2026-05-29 closed");
    }

    [Test]
    public void Format_ReturnsNotConfiguredWhenWorkingHoursAreMissing()
    {
        WorkingHoursPromptFormatter.Format(workingHours: null).ShouldBe("not configured");
    }
}
