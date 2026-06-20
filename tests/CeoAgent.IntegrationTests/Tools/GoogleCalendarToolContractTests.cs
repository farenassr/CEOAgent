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

        ToolExecutionRequest.ForSendPaymentInstructions(new SendPaymentInstructionsRequest())
            .ToolKey.ShouldBe(MvpToolKeys.SendPaymentInstructions);
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
                    CustomerPhoneNumber = "15551234567",
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
                CustomerPhoneNumber = "15551234567",
            },
        }).ToolKey.ShouldBe(MvpToolKeys.UpdateGoogleCalendarReservation);

        ToolExecutionResult.ForCancelGoogleCalendarReservation(new CancelGoogleCalendarReservationResult
        {
            ReservationId = "event-123",
            EventId = "event-123",
            Cancelled = true,
        }).ToolKey.ShouldBe(MvpToolKeys.CancelGoogleCalendarReservation);

        ToolExecutionResult.ForSendPaymentInstructions(new SendPaymentInstructionsResult
        {
            PaymentInstructionsSent = true,
            CustomerVisibleMessageSent = true,
            HandoffRequested = true,
            ReservationEventId = "event-123",
            PaymentMessageId = Guid.Parse("018f4f70-8b5f-7b4c-9d1a-0f6c1d7a2b99"),
        }).ToolKey.ShouldBe(MvpToolKeys.SendPaymentInstructions);
    }
}
