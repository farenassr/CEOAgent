namespace CeoAgent.Application.Company;

public interface ICompanyContextAccessor : ICompanyContext
{
    void SetCompany(Guid companyId);

    void Clear();
}
