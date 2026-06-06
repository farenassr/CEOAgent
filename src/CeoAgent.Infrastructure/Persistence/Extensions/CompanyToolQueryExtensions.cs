using CeoAgent.Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;

namespace CeoAgent.Infrastructure.Persistence.Extensions;

public static class CompanyToolQueryExtensions
{
    public static IQueryable<CompanyTool> EnabledForCompany(
        this IQueryable<CompanyTool> query,
        Guid companyId)
    {
        return query
            .ForCompany(companyId)
            .Where(entity => entity.IsEnabled)
            .OrderBy(entity => entity.ToolKey);
    }

    public static IQueryable<CompanyTool> EnabledForCompanyTool(
        this IQueryable<CompanyTool> query,
        Guid companyId,
        Guid companyToolId)
    {
        return query
            .ForCompany(companyId)
            .Where(entity => entity.Id == companyToolId && entity.IsEnabled);
    }

    public static IQueryable<CompanyTool> WithCredentialReference(
        this IQueryable<CompanyTool> query)
    {
        return query.Include(entity => entity.CredentialReference);
    }
}
