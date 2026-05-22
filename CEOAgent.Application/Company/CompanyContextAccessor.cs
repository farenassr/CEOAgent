namespace CEOAgent.Application.Company;

public sealed class CompanyContextAccessor : ICompanyContextAccessor
{
    private static readonly AsyncLocal<Guid?> CurrentCompany = new();

    public Guid? CompanyId => CurrentCompany.Value;

    public void SetCompany(Guid companyId)
    {
        CurrentCompany.Value = companyId;
    }

    public void Clear()
    {
        CurrentCompany.Value = null;
    }
}
