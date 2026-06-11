using CeoAgent.Application.Abstractions.Organization;
using CeoAgent.Application.Errors;
using CeoAgent.Infrastructure;
using CeoAgent.Infrastructure.Entities;
using CeoAgent.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using CompanyEntity = CeoAgent.Infrastructure.Entities.Company;

namespace CeoAgent.ApiService.Infrastructure.Security;

public sealed class AdminTenantGuard(
    CeoAgentDbContext dbContext,
    IOrganizationContextProvider companyContext) : IAdminTenantGuard
{
    public async Task<CompanyEntity> GetAccessibleCompanyAsync(
        Guid organizationId,
        bool trackChanges,
        CancellationToken cancellationToken)
    {
        var company = await dbContext.Companies
            .WithDefaultTracking(trackChanges)
            .FirstOrDefaultAsync(entity => entity.Id == organizationId, cancellationToken);

        if (companyContext.OrganizationId != organizationId || company is null)
        {
            throw new NotFoundException("company", organizationId);
        }

        return company;
    }

    public async Task EnsureCredentialReferenceAccessibleAsync(
        Guid organizationId,
        Guid? credentialReferenceId,
        CancellationToken cancellationToken)
    {
        if (credentialReferenceId is not { } id)
        {
            return;
        }

        var exists = await dbContext.IntegrationCredentialReferences
            .WithDefaultTracking()
            .AnyAsync(
                entity => entity.OrganizationId == organizationId && entity.Id == id,
                cancellationToken);

        if (!exists)
        {
            throw new NotFoundException("integration_credential_reference", id);
        }
    }
}
