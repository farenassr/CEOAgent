using CEOAgent.Application.Company;

namespace CEOAgent.ApiService.Infrastructure.Company;

public sealed class CompanyContextMiddleware(
    RequestDelegate next,
    ICompanyContextAccessor companyContextAccessor)
{
    public const string HeaderName = "X-Company-Id";

    public async Task InvokeAsync(HttpContext context)
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
