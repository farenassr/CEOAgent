namespace CeoAgent.Application.Company.Abstractions;

public interface ICompanyContextAccessor : ICompanyContext
{
    void SetCompany(Guid companyId);

    void Clear();
}
