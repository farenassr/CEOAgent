namespace CeoAgent.Shared.Response.Payment;

public sealed class BankResponse
{
    public Guid Id { get; set; }

    public required string Name { get; set; }

    public required string CountryCode { get; set; }

    public bool IsActive { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }
}
