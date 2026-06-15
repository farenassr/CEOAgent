using CeoAgent.ApiService.Infrastructure.Security;
using CeoAgent.Infrastructure;
using CeoAgent.Shared.Request.Payment;
using CeoAgent.Shared.Response.Payment;
using FastEndpoints;

namespace CeoAgent.ApiService.Modules.Payments.Endpoints;

public sealed class SetPaymentAccountActiveEndpoint(
    CeoAgentDbContext dbContext,
    IAdminTenantGuard tenantGuard) : Endpoint<SetPaymentAccountActiveRequest, CompanyPaymentAccountResponse>
{
    public override void Configure()
    {
        Patch("/v1/admin/payment-accounts/{paymentAccountId}/active");
        Description(builder => builder
            .WithTags("Payments")
            .WithSummary("Set Payment Account Active")
            .WithDescription("Activates or deactivates a company payment account."));
        Summary(summary =>
        {
            summary.Summary = "Set Payment Account Active";
            summary.Description = "Activates or deactivates a company payment account.";
        });
    }

    public override async Task HandleAsync(SetPaymentAccountActiveRequest request, CancellationToken cancellationToken)
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

        account.IsActive = request.IsActive;
        if (!request.IsActive)
        {
            account.IsDefault = false;
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        await Send.OkAsync(PaymentMapper.ToResponse(account), cancellationToken);
    }
}
