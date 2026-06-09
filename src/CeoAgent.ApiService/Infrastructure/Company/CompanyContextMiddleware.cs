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
        if (context.Items.TryGetValue("CompanyId", out var companyIdValue) &&
            companyIdValue is Guid companyIdFromAdminApiKey)
        {
            companyContextAccessor.SetCompany(companyIdFromAdminApiKey);
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
