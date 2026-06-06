namespace CeoAgent.Application.Abstractions.Company;

public interface ICompanyContextAccessor : ICompanyContext
{
    void SetCompany(Guid companyId);

    void Clear();
}
