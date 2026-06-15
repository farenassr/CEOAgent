using CeoAgent.Infrastructure.Entities;

namespace CeoAgent.Infrastructure.Persistence.Extensions;

public static class BankQueryExtensions
{
    public static IQueryable<Bank> Active(this IQueryable<Bank> query)
    {
        return query.Where(bank => bank.IsActive);
    }

    public static IQueryable<Bank> OrderedForCatalog(this IQueryable<Bank> query)
    {
        return query
            .OrderBy(bank => bank.CountryCode)
            .ThenBy(bank => bank.Name);
    }

    public static IQueryable<Bank> WithId(this IQueryable<Bank> query, Guid bankId)
    {
        return query.Where(bank => bank.Id == bankId);
    }
}
