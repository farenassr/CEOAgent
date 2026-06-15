using CeoAgent.Shared.Payment;

namespace CeoAgent.Shared.Response.Payment;

public sealed class CompanyPaymentAccountResponse
{
    public Guid Id { get; set; }

    public Guid OrganizationId { get; set; }

    public Guid BankId { get; set; }

    public required string BankName { get; set; }

    public required string AccountNumber { get; set; }

    public PaymentAccountType AccountType { get; set; }

    public string? AccountHolderName { get; set; }

    public required string Currency { get; set; }

    public decimal ReservationPaymentAmount { get; set; }

    public required string QrBlobContainer { get; set; }

    public required string QrBlobName { get; set; }

    public string? QrBlobUri { get; set; }

    public bool IsDefault { get; set; }

    public bool IsActive { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }
}
