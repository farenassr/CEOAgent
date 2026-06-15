namespace CeoAgent.Shared.Response.Payment;

public sealed class BankListResponse
{
    public IReadOnlyList<BankResponse> Banks { get; set; } = [];
}
