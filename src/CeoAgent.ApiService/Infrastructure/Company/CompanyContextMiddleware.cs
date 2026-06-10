using CeoAgent.Application.Abstractions.Company;
using CeoAgent.Infrastructure.Implementation.Company;

namespace CeoAgent.ApiService.Infrastructure.Company;

public sealed class CompanyContextMiddleware(
    RequestDelegate next)
{
    public const string HeaderName = "X-Company-Id";

    public async Task InvokeAsync(
        HttpContext context,
        ICompanyContextAccessor companyContextAccessor)
    {
        var companyIdClaim = context.User.FindFirst("company_id")?.Value;
        if (Guid.TryParse(companyIdClaim, out var companyId))
        {
            companyContextAccessor.SetCompany(companyId);
        }

        try
        {
            await next(context);
        }
        finally
        {
            companyContextAccessor.Clear();
        }
    }
}
