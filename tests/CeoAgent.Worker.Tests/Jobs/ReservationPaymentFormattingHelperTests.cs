using CeoAgent.Shared.Helper;
using Shouldly;

namespace CeoAgent.Worker.Tests.Jobs;

public sealed class ReservationPaymentFormattingHelperTests
{
    [Test]
    public void FormatPaymentAmount_UsesInvariantCultureWithoutTrailingZeroes()
    {
        ReservationPaymentFormattingHelper.FormatPaymentAmount(50000m).ShouldBe("50000");
        ReservationPaymentFormattingHelper.FormatPaymentAmount(125.5m).ShouldBe("125.5");
        ReservationPaymentFormattingHelper.FormatPaymentAmount(125.25m).ShouldBe("125.25");
    }

    [Test]
    public void FormatReservationDate_UsesTwelveHourClockWithLowercaseMeridiem()
    {
        var morning = new DateTimeOffset(2026, 5, 28, 7, 0, 0, TimeSpan.FromHours(-5));
        var evening = new DateTimeOffset(2026, 5, 28, 19, 0, 0, TimeSpan.FromHours(-5));

        ReservationPaymentFormattingHelper.FormatReservationDate(morning)
            .ShouldBe("2026-05-28 7:00 am -05:00");
        ReservationPaymentFormattingHelper.FormatReservationDate(evening)
            .ShouldBe("2026-05-28 7:00 pm -05:00");
    }

    [Test]
    public void FormatReservationDate_WhenDateIsMissing_ReturnsUnavailable()
    {
        ReservationPaymentFormattingHelper.FormatReservationDate(null).ShouldBe("no disponible");
    }
}
