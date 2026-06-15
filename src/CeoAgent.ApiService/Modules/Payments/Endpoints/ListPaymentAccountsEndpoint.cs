using CeoAgent.ApiService.Infrastructure.Security;
using CeoAgent.Infrastructure;
using CeoAgent.Infrastructure.Persistence.Extensions;
using CeoAgent.Shared.Response.Payment;
using FastEndpoints;
using Microsoft.EntityFrameworkCore;

namespace CeoAgent.ApiService.Modules.Payments.Endpoints;

public sealed class ListPaymentAccountsEndpoint(
    CeoAgentDbContext dbContext,
    IAdminTenantGuard tenantGuard) : EndpointWithoutRequest<CompanyPaymentAccountListResponse>
{
    public override void Configure()
    {
        Get("/v1/admin/payment-accounts");
        Description(builder => builder
            .WithTags("Payments")
            .WithSummary("List Company Payment Accounts")
            .WithDescription("Lists bank payment accounts for the authenticated company."));
        Summary(summary =>
        {
            summary.Summary = "List Company Payment Accounts";
            summary.Description = "Lists bank payment accounts for the authenticated company.";
        });
    }

    public override async Task HandleAsync(CancellationToken cancellationToken)
    {
        var organizationId = tenantGuard.RequireAuthenticatedOrganizationId();
        await tenantGuard.GetAuthenticatedCompanyAsync(trackChanges: false, cancellationToken);

        var accounts = await dbContext.CompanyPaymentAccounts
            .AsNoTracking()
            .WithBank()
            .ForOrganization(organizationId)
            .OrderedForAdminList()
            .Select(account => PaymentMapper.ToResponse(account))
            .ToListAsync(cancellationToken);

        await Send.OkAsync(new CompanyPaymentAccountListResponse { Accounts = accounts }, cancellationToken);
    }
}
