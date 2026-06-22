using CeoAgent.Infrastructure.Entities;
using CeoAgent.Shared.Helper;

namespace CeoAgent.Infrastructure.Implementation.AITools.Payments;

public static class PaymentInstructionCaptionBuilder
{
    public static string Build(CompanyPaymentAccount account, ToolExecution reservationExecution)
    {
        var reservationRequest = reservationExecution.Request?.CreateCalendarEvent;
        var start = ReservationPaymentFormattingHelper.FormatReservationDate(reservationRequest?.Start);
        var amount = ReservationPaymentFormattingHelper.FormatPaymentAmount(account.ReservationPaymentAmount);
        var summary = string.IsNullOrWhiteSpace(reservationRequest?.Summary)
            ? "Reserva"
            : reservationRequest.Summary.Trim();
        var customerName = string.IsNullOrWhiteSpace(reservationRequest?.CustomerName)
            ? "Cliente"
            : reservationRequest.CustomerName.Trim();

        return $@"✅ *¡Tu reserva ha sido creada!*
_{summary}_

*📌 DETALLES DE LA RESERVA*
👤 *Cliente:* {customerName}
📅 *Hora de la reserva:* {start}

*💳 DATOS DE PAGO*
💰 *{amount} {account.Currency}*
🏦 *Banco:* {account.Bank.Name} ({account.AccountType}) Número de cuenta: `{account.AccountNumber}`

📸 *QR:* Escanea el código adjunto para pagar.

---
💡 _El dinero de la reserva es consumible durante tu visita._
⚠️ _La reserva queda finalmente confirmada al recibir el pago._";
    }
}
