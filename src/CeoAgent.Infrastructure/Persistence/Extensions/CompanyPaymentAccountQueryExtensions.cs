using CeoAgent.Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;

namespace CeoAgent.Infrastructure.Persistence.Extensions;

public static class CompanyPaymentAccountQueryExtensions
{
    public static IQueryable<CompanyPaymentAccount> ForCurrency(
        this IQueryable<CompanyPaymentAccount> query,
        string currency)
    {
        return query.Where(account => account.Currency == currency);
    }

    public static IQueryable<CompanyPaymentAccount> ActiveDefaults(
        this IQueryable<CompanyPaymentAccount> query)
    {
        return query.Where(account => account.IsDefault && account.IsActive);
    }

    public static IQueryable<CompanyPaymentAccount> ExceptAccount(
        this IQueryable<CompanyPaymentAccount> query,
        Guid? accountId)
    {
        return accountId.HasValue
            ? query.Where(account => account.Id != accountId.Value)
            : query;
    }

    public static IQueryable<CompanyPaymentAccount> WithBank(
        this IQueryable<CompanyPaymentAccount> query)
    {
        return query.Include(account => account.Bank);
    }

    public static IQueryable<CompanyPaymentAccount> OrderedForAdminList(
        this IQueryable<CompanyPaymentAccount> query)
    {
        return query
            .OrderByDescending(account => account.IsDefault)
            .ThenByDescending(account => account.IsActive)
            .ThenBy(account => account.Currency)
            .ThenBy(account => account.Bank.Name);
    }

    public static IQueryable<CompanyPaymentAccount> WithId(
        this IQueryable<CompanyPaymentAccount> query,
        Guid paymentAccountId)
    {
        return query.Where(account => account.Id == paymentAccountId);
    }
}
