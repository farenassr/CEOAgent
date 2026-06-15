namespace CeoAgent.Infrastructure.Entities;

public sealed class Bank
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public required string Name { get; set; }

    public required string CountryCode { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public ICollection<CompanyPaymentAccount> PaymentAccounts { get; } = [];
}
