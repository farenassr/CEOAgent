using CeoAgent.Application.Errors;
using CeoAgent.Infrastructure;
using CeoAgent.Infrastructure.Entities;
using CeoAgent.Infrastructure.Persistence;
using CeoAgent.Infrastructure.Persistence.Extensions;
using Microsoft.EntityFrameworkCore;

namespace CeoAgent.ApiService.Modules.Payments;

internal static class PaymentEndpointHelpers
{
    public static async Task EnsureActiveBankAsync(
        CeoAgentDbContext dbContext,
        Guid bankId,
        CancellationToken cancellationToken)
    {
        var exists = await dbContext.Banks
            .AsNoTracking()
            .WithId(bankId)
            .Active()
            .AnyAsync(cancellationToken);
        if (!exists)
        {
            throw new NotFoundException("bank", bankId);
        }
    }

    public static async Task ClearOtherDefaultAccountsAsync(
        CeoAgentDbContext dbContext,
        Guid organizationId,
        string currency,
        Guid? exceptAccountId,
        CancellationToken cancellationToken)
    {
        var accounts = await dbContext.CompanyPaymentAccounts
            .ForOrganization(organizationId)
            .ForCurrency(currency)
            .ActiveDefaults()
            .ExceptAccount(exceptAccountId)
            .ToListAsync(cancellationToken);

        foreach (var account in accounts)
        {
            account.IsDefault = false;
        }
    }

    public static async Task<CompanyPaymentAccount> GetPaymentAccountAsync(
        CeoAgentDbContext dbContext,
        Guid organizationId,
        Guid paymentAccountId,
        bool trackChanges,
        CancellationToken cancellationToken)
    {
        var account = await dbContext.CompanyPaymentAccounts
            .WithBank()
            .WithDefaultTracking(trackChanges)
            .ForOrganization(organizationId)
            .WithId(paymentAccountId)
            .SingleOrDefaultAsync(cancellationToken);

        return account ?? throw new NotFoundException("company_payment_account", paymentAccountId);
    }
}
