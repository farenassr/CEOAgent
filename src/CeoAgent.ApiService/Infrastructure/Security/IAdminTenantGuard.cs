using CompanyEntity = CeoAgent.Infrastructure.Entities.Company;

namespace CeoAgent.ApiService.Infrastructure.Security;

public interface IAdminTenantGuard
{
    Task<CompanyEntity> GetAccessibleCompanyAsync(
        Guid companyId,
        bool trackChanges,
        CancellationToken cancellationToken);

    Task EnsureCredentialReferenceAccessibleAsync(
        Guid companyId,
        Guid? credentialReferenceId,
        CancellationToken cancellationToken);
}
