using System;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace CeoAgent.ApiService.Infrastructure.Security;

public static class AdminApiKeyMiddlewareExtensions
{
    public static IApplicationBuilder UseAdminApiKey(this IApplicationBuilder app)
    {
        return app.Use(async (context, next) =>
        {
            if (!context.Request.Path.StartsWithSegments("/v1/admin", StringComparison.Ordinal))
            {
                await next(context);
                return;
            }

            var options = context.RequestServices
                .GetRequiredService<IOptions<AdminApiKeyOptions>>()
                .Value;

            if (!context.Request.Headers.TryGetValue("X-Admin-Api-Key", out var providedApiKey))
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                context.Response.ContentType = "text/plain";
                await context.Response.WriteAsync("Missing admin API key.");
                return;
            }

            if (!IsValidApiKey(providedApiKey.ToString(), options.Key))
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                context.Response.ContentType = "text/plain";
                await context.Response.WriteAsync("Invalid admin API key.");
                return;
            }

            context.Items["CompanyId"] = options.CompanyId;
            context.User = new ClaimsPrincipal(new ClaimsIdentity(
                [
                    new Claim(ClaimTypes.NameIdentifier, "admin-api-key"),
                ],
                authenticationType: "AdminApiKey"));

            await next(context);
        });
    }

    private static bool IsValidApiKey(string providedApiKey, string configuredApiKey)
    {
        if (string.IsNullOrWhiteSpace(providedApiKey) || string.IsNullOrWhiteSpace(configuredApiKey))
        {
            return false;
        }

        var providedHash = SHA256.HashData(Encoding.UTF8.GetBytes(providedApiKey));
        var configuredHash = SHA256.HashData(Encoding.UTF8.GetBytes(configuredApiKey));
        return CryptographicOperations.FixedTimeEquals(providedHash, configuredHash);
    }
}
