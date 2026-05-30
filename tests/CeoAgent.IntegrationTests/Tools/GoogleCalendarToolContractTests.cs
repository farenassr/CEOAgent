using CeoAgent.Infrastructure.Entities.JsonDocuments;
using CeoAgent.Shared.Constants;
using Shouldly;

namespace CeoAgent.IntegrationTests.Tools;

public sealed class GoogleCalendarToolContractTests
{
    [Test]
    public void ToolExecutionRequest_FactoriesUseOfficialGoogleCalendarToolNames()
    {
        ToolExecutionRequest.ForCheckGoogleCalendarAvailability(new CheckAvailabilityRequest
        {
            Date = new DateOnly(2026, 5, 28),
            PartySize = 4,
            PreferredTime = new TimeOnly(16, 0),
        }).ToolKey.ShouldBe(MvpToolKeys.CheckGoogleCalendarAvailability);

        ToolExecutionRequest.ForCreateGoogleCalendarReservation(new CreateCalendarEventRequest
        {
            Start = new DateTimeOffset(2026, 5, 28, 16, 0, 0, TimeSpan.FromHours(-5)),
            End = new DateTimeOffset(2026, 5, 28, 17, 0, 0, TimeSpan.FromHours(-5)),
            Summary = "Reservation for 4",
        }).ToolKey.ShouldBe(MvpToolKeys.CreateGoogleCalendarReservation);
    }

    [Test]
    public void ToolExecutionResult_FactoriesUseOfficialGoogleCalendarToolNames()
    {
        ToolExecutionResult.ForCheckGoogleCalendarAvailability(new CheckAvailabilityResult
        {
            Available = false,
            AlternativeSlots = [new TimeOnly(16, 30)],
            UnavailabilityReason = "slot_unavailable",
        }).ToolKey.ShouldBe(MvpToolKeys.CheckGoogleCalendarAvailability);

        ToolExecutionResult.ForCreateGoogleCalendarReservation(new CreateCalendarEventResult
        {
            EventId = "event-123",
            EventUrl = "https://calendar.google.com/event?eid=event-123",
        }).ToolKey.ShouldBe(MvpToolKeys.CreateGoogleCalendarReservation);
    }
}
