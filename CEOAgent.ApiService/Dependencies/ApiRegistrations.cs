using FastEndpoints;
using System.Threading.RateLimiting;

namespace CEOAgent.ApiService.Dependencies;

public static class ApiRegistrations
{
    private const string CorsPolicyName = "configured-origins";

    public static IServiceCollection AddApi(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddFastEndpoints(options => options.IncludeAbstractValidators = true);

        services.AddMediator(options => options.ServiceLifetime = ServiceLifetime.Scoped);
        services.AddCors(options =>
        {
            options.AddPolicy(CorsPolicyName, policy =>
            {
                string[] allowedOrigins = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];

                if (allowedOrigins.Length > 0)
                {
                    policy.WithOrigins(allowedOrigins)
                        .AllowAnyHeader()
                        .AllowAnyMethod();
                }
            });
        });
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
            {
                var partitionKey = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";

                return RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey,
                    _ => new FixedWindowRateLimiterOptions
                    {
                        AutoReplenishment = true,
                        PermitLimit = 120,
                        QueueLimit = 0,
                        Window = TimeSpan.FromMinutes(1),
                    });
            });
        });
        services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders = Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedFor
                | Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedProto;
        });

        return services;
    }

    public static IApplicationBuilder UseConfiguredCors(this IApplicationBuilder app)
    {
        return app.UseCors(CorsPolicyName);
    }
}
