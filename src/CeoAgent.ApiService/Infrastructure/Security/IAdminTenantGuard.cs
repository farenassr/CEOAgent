using CompanyEntity = CeoAgent.Infrastructure.Entities.Company;

namespace CeoAgent.ApiService.Infrastructure.Security;

public interface IAdminTenantGuard
{
    Task<CompanyEntity> GetAccessibleCompanyAsync(
        Guid organizationId,
        bool trackChanges,
        CancellationToken cancellationToken);

    Task EnsureCredentialReferenceAccessibleAsync(
        Guid organizationId,
        Guid? credentialReferenceId,
        CancellationToken cancellationToken);
}
