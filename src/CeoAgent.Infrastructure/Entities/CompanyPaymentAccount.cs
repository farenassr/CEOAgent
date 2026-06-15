using CeoAgent.Shared.Payment;

namespace CeoAgent.Infrastructure.Entities;

public sealed class CompanyPaymentAccount : AuditableOrganizationOwnedEntity
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid BankId { get; set; }

    public required string AccountNumber { get; set; }

    public PaymentAccountType AccountType { get; set; }

    public string? AccountHolderName { get; set; }

    public required string Currency { get; set; }

    public decimal ReservationPaymentAmount { get; set; }

    public required string QrBlobContainer { get; set; }

    public required string QrBlobName { get; set; }

    public string? QrBlobUri { get; set; }

    public bool IsDefault { get; set; }

    public bool IsActive { get; set; } = true;

    public Bank Bank { get; set; } = null!;

    public Company Company { get; set; } = null!;
}
