using CompanyEntity = CeoAgent.Infrastructure.Entities.Company;

namespace CeoAgent.ApiService.Infrastructure.Security;

public interface IAdminTenantGuard
{
    Guid RequireAuthenticatedOrganizationId();

    Task<CompanyEntity> GetAuthenticatedCompanyAsync(
        bool trackChanges,
        CancellationToken cancellationToken);

    Task<CompanyEntity> GetAccessibleCompanyAsync(
        Guid organizationId,
        bool trackChanges,
        CancellationToken cancellationToken);

    Task EnsureCredentialReferenceAccessibleAsync(
        Guid organizationId,
        Guid? credentialReferenceId,
        CancellationToken cancellationToken);
}
