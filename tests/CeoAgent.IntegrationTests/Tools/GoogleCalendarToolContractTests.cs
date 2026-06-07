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
            CustomerName = "Ada Lovelace",
        }).ToolKey.ShouldBe(MvpToolKeys.CreateGoogleCalendarReservation);

        ToolExecutionRequest.ForFindGoogleCalendarReservations(new FindGoogleCalendarReservationsRequest
        {
            Date = new DateOnly(2026, 5, 28),
            IncludePast = false,
            Status = null,
        }).ToolKey.ShouldBe(MvpToolKeys.FindGoogleCalendarReservations);

        ToolExecutionRequest.ForUpdateGoogleCalendarReservation(new UpdateGoogleCalendarReservationRequest
        {
            ReservationId = "event-123",
            NewStart = new DateTimeOffset(2026, 5, 28, 20, 0, 0, TimeSpan.FromHours(-5)),
            NewEnd = new DateTimeOffset(2026, 5, 28, 21, 0, 0, TimeSpan.FromHours(-5)),
            Summary = null,
            CustomerName = null,
        }).ToolKey.ShouldBe(MvpToolKeys.UpdateGoogleCalendarReservation);

        ToolExecutionRequest.ForCancelGoogleCalendarReservation(new CancelGoogleCalendarReservationRequest
        {
            ReservationId = "event-123",
            Reason = null,
        }).ToolKey.ShouldBe(MvpToolKeys.CancelGoogleCalendarReservation);
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

        ToolExecutionResult.ForFindGoogleCalendarReservations(new FindGoogleCalendarReservationsResult
        {
            Count = 1,
            DisambiguationNeeded = false,
            Reservations =
            [
                new GoogleCalendarReservationResultItem
                {
                    ReservationId = "event-123",
                    EventId = "event-123",
                    Start = new DateTimeOffset(2026, 5, 28, 16, 0, 0, TimeSpan.FromHours(-5)),
                    End = new DateTimeOffset(2026, 5, 28, 17, 0, 0, TimeSpan.FromHours(-5)),
                    Summary = "Reservation for 4",
                    CustomerName = "Ada Lovelace",
                    EventUrl = "https://calendar.google.com/event?eid=event-123",
                },
            ],
        }).ToolKey.ShouldBe(MvpToolKeys.FindGoogleCalendarReservations);

        ToolExecutionResult.ForUpdateGoogleCalendarReservation(new UpdateGoogleCalendarReservationResult
        {
            Reservation = new GoogleCalendarReservationResultItem
            {
                ReservationId = "event-123",
                EventId = "event-123",
                Start = new DateTimeOffset(2026, 5, 28, 20, 0, 0, TimeSpan.FromHours(-5)),
                End = new DateTimeOffset(2026, 5, 28, 21, 0, 0, TimeSpan.FromHours(-5)),
            },
        }).ToolKey.ShouldBe(MvpToolKeys.UpdateGoogleCalendarReservation);

        ToolExecutionResult.ForCancelGoogleCalendarReservation(new CancelGoogleCalendarReservationResult
        {
            ReservationId = "event-123",
            EventId = "event-123",
            Cancelled = true,
        }).ToolKey.ShouldBe(MvpToolKeys.CancelGoogleCalendarReservation);
    }
}
