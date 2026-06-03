using CeoAgent.Application.Company.Abstractions;

namespace CeoAgent.Application.Company.Implementation;

public sealed class CompanyContextAccessor : ICompanyContextAccessor
{
    public Guid? CompanyId { get; private set; }

    public void SetCompany(Guid companyId)
    {
        CompanyId = companyId;
    }

    public void Clear()
    {
        CompanyId = null;
    }
}
