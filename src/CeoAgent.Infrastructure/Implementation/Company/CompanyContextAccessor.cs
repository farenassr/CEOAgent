using CeoAgent.Application.Abstractions.Company;

namespace CeoAgent.Infrastructure.Implementation.Company;

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
