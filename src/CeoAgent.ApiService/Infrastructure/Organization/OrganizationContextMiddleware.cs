using CeoAgent.Application.Abstractions.Organization;

namespace CeoAgent.ApiService.Infrastructure.Organization;

public sealed class OrganizationContextMiddleware(
    RequestDelegate next)
{
    public async Task InvokeAsync(
        HttpContext context,
        IOrganizationContextAccessor organizationContextAccessor,
        AuthenticatedOrganizationContextProvider authenticatedOrganizationContextProvider)
    {
        var isPublicPath = IsPublicPath(context.Request.Path);
        var isAuthenticated = context.User.Identity?.IsAuthenticated == true;

        if (isAuthenticated
            && authenticatedOrganizationContextProvider.TryGetOrganizationId(context, out var organizationId))
        {
            organizationContextAccessor.SetOrganization(organizationId);
        }
        else if (isAuthenticated && !isPublicPath)
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return;
        }

        try
        {
            await next(context);
        }
        finally
        {
            organizationContextAccessor.Clear();
        }
    }

    private static bool IsPublicPath(PathString path)
    {
        return path.StartsWithSegments("/health", StringComparison.OrdinalIgnoreCase)
            || path.StartsWithSegments("/alive", StringComparison.OrdinalIgnoreCase)
            || path.StartsWithSegments("/v1/whatsapp", StringComparison.OrdinalIgnoreCase)
            || path.StartsWithSegments("/openapi", StringComparison.OrdinalIgnoreCase)
            || path.StartsWithSegments("/scalar", StringComparison.OrdinalIgnoreCase)
            || path.StartsWithSegments("/__test", StringComparison.OrdinalIgnoreCase);
    }
}
