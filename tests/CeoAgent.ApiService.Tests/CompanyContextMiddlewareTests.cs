using CeoAgent.ApiService.Infrastructure.Company;
using CeoAgent.Infrastructure.Implementation.Company;
using Microsoft.AspNetCore.Http;
using Shouldly;
using System.Security.Claims;

namespace CeoAgent.ApiService.Tests;

public sealed class CompanyContextMiddlewareTests
{
    [Test]
    public async Task InvokeAsync_WhenPublicRequestContainsCompanyHeader_DoesNotSetCompanyContext()
    {
        var accessor = new CompanyContextAccessor();
        var observedCompanyId = Guid.NewGuid();
        var middleware = new CompanyContextMiddleware(context =>
        {
            observedCompanyId = accessor.CompanyId ?? Guid.Empty;
            return Task.CompletedTask;
        });
        var context = new DefaultHttpContext();
        context.Request.Path = "/v1/whatsapp";
        context.Request.Headers[CompanyContextMiddleware.HeaderName] = Guid.NewGuid().ToString();

        await middleware.InvokeAsync(context, accessor);

        observedCompanyId.ShouldBe(Guid.Empty);
        accessor.CompanyId.ShouldBeNull();
    }

    [Test]
    public async Task InvokeAsync_WhenJwtContainsCompanyIdClaim_SetsCompanyContextForRequestOnly()
    {
        var companyId = Guid.NewGuid();
        var accessor = new CompanyContextAccessor();
        var observedCompanyId = Guid.Empty;
        var middleware = new CompanyContextMiddleware(context =>
        {
            observedCompanyId = accessor.CompanyId ?? Guid.Empty;
            return Task.CompletedTask;
        });
        var context = new DefaultHttpContext();
        context.Request.Path = "/v1/admin/companies";
        context.User = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim("company_id", companyId.ToString())],
            authenticationType: "Bearer"));

        await middleware.InvokeAsync(context, accessor);

        observedCompanyId.ShouldBe(companyId);
        accessor.CompanyId.ShouldBeNull();
    }
}
