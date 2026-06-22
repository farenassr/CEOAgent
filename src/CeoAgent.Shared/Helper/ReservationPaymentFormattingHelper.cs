using System.Globalization;

namespace CeoAgent.Shared.Helper;

public static class ReservationPaymentFormattingHelper
{
    public static string FormatPaymentAmount(decimal amount)
    {
        return amount.ToString("0.##", CultureInfo.InvariantCulture);
    }

    public static string FormatReservationDate(DateTimeOffset? date)
    {
        return date is null
            ? "no disponible"
            : date.Value
                .ToString("yyyy-MM-dd h:mm tt zzz", CultureInfo.InvariantCulture)
                .ToLowerInvariant();
    }
}
