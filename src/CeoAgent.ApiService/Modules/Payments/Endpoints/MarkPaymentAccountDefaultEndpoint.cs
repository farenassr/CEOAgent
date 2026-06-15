using CeoAgent.ApiService.Infrastructure.Security;
using CeoAgent.Infrastructure;
using CeoAgent.Shared.Response.Payment;
using FastEndpoints;

namespace CeoAgent.ApiService.Modules.Payments.Endpoints;

public sealed class MarkPaymentAccountDefaultEndpoint(
    CeoAgentDbContext dbContext,
    IAdminTenantGuard tenantGuard) : EndpointWithoutRequest<CompanyPaymentAccountResponse>
{
    public override void Configure()
    {
        Post("/v1/admin/payment-accounts/{paymentAccountId}/default");
        Description(builder => builder
            .WithTags("Payments")
            .WithSummary("Mark Payment Account Default")
            .WithDescription("Marks a company payment account as the active default for its currency."));
        Summary(summary =>
        {
            summary.Summary = "Mark Payment Account Default";
            summary.Description = "Marks a company payment account as the active default for its currency.";
        });
    }

    public override async Task HandleAsync(CancellationToken cancellationToken)
    {
        var organizationId = tenantGuard.RequireAuthenticatedOrganizationId();
        await tenantGuard.GetAuthenticatedCompanyAsync(trackChanges: false, cancellationToken);
        var paymentAccountId = Route<Guid>("paymentAccountId");
        var account = await PaymentEndpointHelpers.GetPaymentAccountAsync(
            dbContext,
            organizationId,
            paymentAccountId,
            trackChanges: true,
            cancellationToken);

        account.IsActive = true;
        account.IsDefault = true;
        await PaymentEndpointHelpers.ClearOtherDefaultAccountsAsync(
            dbContext,
            organizationId,
            account.Currency,
            account.Id,
            cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        await Send.OkAsync(PaymentMapper.ToResponse(account), cancellationToken);
    }
}
