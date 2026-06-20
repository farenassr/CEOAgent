using CeoAgent.Application.Agents;
using Shouldly;

namespace CeoAgent.Worker.Tests.Agents;

public sealed class AgentPromptBuilderTests
{
    [Test]
    public void Build_IncludesReservationChangeToolRoutingRule()
    {
        var prompt = AgentPromptBuilder.Build(new AgentPromptContext
        {
            CompanyName = "Contoso Bistro",
            TimeZoneId = "America/Bogota",
            LocalNow = new DateTimeOffset(2026, 6, 20, 12, 0, 0, TimeSpan.FromHours(-5)),
            AgentDisplayName = "Contoso Assistant",
            Language = "es",
            ModelName = "gpt-4.1-mini",
            WorkingHoursSummary = "Saturday 12:00-22:00",
        });

        prompt.ShouldContain("For changes or cancellations of an existing reservation");
        prompt.ShouldContain("find_google_calendar_reservations");
        prompt.ShouldContain("update_google_calendar_reservation");
        prompt.ShouldContain("cancel_google_calendar_reservation");
        prompt.ShouldContain("do not use check_google_calendar_availability to pre-check the new time");
    }
}
