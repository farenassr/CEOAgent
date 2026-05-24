using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using System.Threading.RateLimiting;

namespace CEOAgent.ApiService.Dependencies;

internal sealed class ConfigureCorsOptions(IOptions<ApiOptions> apiOptions) : IConfigureOptions<CorsOptions>
{
    public void Configure(CorsOptions options)
    {
        options.AddPolicy(ApiRegistrations.CorsPolicyName, policy =>
        {
            var allowedOrigins = apiOptions.Value.Cors.AllowedOrigins;

            if (allowedOrigins.Length > 0)
            {
                policy.WithOrigins(allowedOrigins)
                    .AllowAnyHeader()
                    .AllowAnyMethod();
            }
        });
    }
}

internal sealed class ConfigureRateLimiterOptions(IOptions<ApiOptions> apiOptions) : IConfigureOptions<RateLimiterOptions>
{
    public void Configure(RateLimiterOptions options)
    {
        var rateLimiting = apiOptions.Value.RateLimiting;

        options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
        options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
        {
            var partitionKey = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";

            return RateLimitPartition.GetFixedWindowLimiter(
                partitionKey,
                _ => new FixedWindowRateLimiterOptions
                {
                    AutoReplenishment = rateLimiting.AutoReplenishment,
                    PermitLimit = rateLimiting.PermitLimit,
                    QueueLimit = rateLimiting.QueueLimit,
                    Window = TimeSpan.FromSeconds(rateLimiting.WindowSeconds),
                });
        });
    }
}

internal sealed class ConfigureForwardedHeadersOptions : IConfigureOptions<ForwardedHeadersOptions>
{
    public void Configure(ForwardedHeadersOptions options)
    {
        options.ForwardedHeaders = ForwardedHeaders.XForwardedFor
            | ForwardedHeaders.XForwardedProto;
    }
}
