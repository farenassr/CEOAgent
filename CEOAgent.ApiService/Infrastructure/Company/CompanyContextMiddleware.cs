using CeoAgent.Application.Company.Abstractions;
using CeoAgent.Application.Company.Implementation;

namespace CeoAgent.ApiService.Infrastructure.Company;

public sealed class CompanyContextMiddleware(
    RequestDelegate next)
{
    public const string HeaderName = "X-Company-Id";

    public async Task InvokeAsync(
        HttpContext context,
        ICompanyContextAccessor companyContextAccessor)
    {
        if (context.Items.TryGetValue("CompanyId", out var companyIdValue) &&
            companyIdValue is Guid companyIdFromAdminApiKey)
        {
            companyContextAccessor.SetCompany(companyIdFromAdminApiKey);
        }
        else if (!context.Request.Path.StartsWithSegments("/v1/admin", StringComparison.Ordinal))
        {
            if (context.Request.Headers.TryGetValue(HeaderName, out var values)
                && Guid.TryParse(values.FirstOrDefault(), out var companyId))
            {
                companyContextAccessor.SetCompany(companyId);
            }
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
