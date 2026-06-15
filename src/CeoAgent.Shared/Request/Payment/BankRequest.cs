namespace CeoAgent.Shared.Request.Payment;

public sealed class BankRequest
{
    public required string Name { get; set; }

    public required string CountryCode { get; set; }

    public bool IsActive { get; set; } = true;
}
