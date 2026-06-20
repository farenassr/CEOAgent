using CeoAgent.Infrastructure.Entities;
using System.Globalization;

namespace CeoAgent.Infrastructure.Implementation.AITools.Payments;

public static class PaymentInstructionCaptionBuilder
{
    public static string Build(CompanyPaymentAccount account, ToolExecution reservationExecution)
    {
        var reservationRequest = reservationExecution.Request?.CreateCalendarEvent;
        var reservationResult = reservationExecution.Result?.CreateCalendarEvent;
        var start = reservationRequest?.Start.ToString("yyyy-MM-dd HH:mm zzz", CultureInfo.InvariantCulture) ?? "no disponible";
        var end = reservationRequest?.End.ToString("yyyy-MM-dd HH:mm zzz", CultureInfo.InvariantCulture) ?? "no disponible";
        var summary = string.IsNullOrWhiteSpace(reservationRequest?.Summary)
            ? "Reserva"
            : reservationRequest.Summary.Trim();
        var customerName = string.IsNullOrWhiteSpace(reservationRequest?.CustomerName)
            ? "Cliente"
            : reservationRequest.CustomerName.Trim();

        return string.Join(
            Environment.NewLine,
            "La reserva fue creada.",
            $"Reserva: {summary}",
            $"Cliente: {customerName}",
            $"Inicio: {start}",
            $"Fin: {end}",
            $"Codigo de reserva: {reservationResult?.EventId ?? "no disponible"}",
            "Datos de pago:",
            $"Banco: {account.Bank.Name}",
            $"Tipo de cuenta: {account.AccountType}",
            $"Numero de cuenta: {account.AccountNumber}",
            $"Monto: {account.ReservationPaymentAmount.ToString("0.##", CultureInfo.InvariantCulture)} {account.Currency}",
            "QR: escanea el codigo QR adjunto para pagar.",
            "El dinero de la reserva es consumible durante tu visita.",
            "La reserva queda finalmente confirmada cuando recibamos el pago.");
    }
}
