using CeoAgent.Shared.Payment;

namespace CeoAgent.Shared.Request.Payment;

public sealed class CompanyPaymentAccountRequest
{
    public Guid BankId { get; set; }

    public required string AccountNumber { get; set; }

    public PaymentAccountType AccountType { get; set; }

    public string? AccountHolderName { get; set; }

    public required string Currency { get; set; }

    public decimal ReservationPaymentAmount { get; set; }

    public bool IsDefault { get; set; }

    public bool IsActive { get; set; } = true;
}
