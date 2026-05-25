using CeoAgent.Application.Company;

namespace CeoAgent.ApiService.Infrastructure.Company;

public sealed class CompanyContextMiddleware(
    RequestDelegate next)
{
    public const string HeaderName = "X-Company-Id";

    public async Task InvokeAsync(
        HttpContext context,
        ICompanyContextAccessor companyContextAccessor)
    {
        if (context.Request.Headers.TryGetValue(HeaderName, out var values)
            && Guid.TryParse(values.FirstOrDefault(), out var companyId))
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
