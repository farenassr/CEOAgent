using CeoAgent.Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;

namespace CeoAgent.Infrastructure.Persistence.Extensions;

public static class CompanyToolQueryExtensions
{
    public static IQueryable<CompanyTool> EnabledForOrganization(
        this IQueryable<CompanyTool> query,
        Guid organizationId)
    {
        return query
            .ForOrganization(organizationId)
            .Where(entity => entity.IsEnabled)
            .OrderBy(entity => entity.ToolKey);
    }

    public static IQueryable<CompanyTool> EnabledForOrganizationTool(
        this IQueryable<CompanyTool> query,
        Guid organizationId,
        Guid companyToolId)
    {
        return query
            .ForOrganization(organizationId)
            .Where(entity => entity.Id == companyToolId && entity.IsEnabled);
    }

    public static IQueryable<CompanyTool> WithCredentialReference(
        this IQueryable<CompanyTool> query)
    {
        return query.Include(entity => entity.CredentialReference);
    }
}
