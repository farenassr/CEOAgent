namespace CeoAgent.Shared.Response.Payment;

public sealed class CompanyPaymentAccountListResponse
{
    public IReadOnlyList<CompanyPaymentAccountResponse> Accounts { get; set; } = [];
}
