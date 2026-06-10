using FastEndpoints;
using CeoAgent.ApiService.Infrastructure.Security;
using CeoAgent.ApiService.Modules.GoogleCalendar;

namespace CeoAgent.ApiService.Dependencies;

public static class ApiRegistrations
{
    internal const string CorsPolicyName = "configured-origins";

    public static IServiceCollection AddApi(this IServiceCollection services)
    {
        services.AddOptions<ApiOptions>()
            .BindConfiguration(ApiOptions.SectionName)
            .Validate(ApiOptions.IsValid, "Api rate limiting options must be positive.")
            .ValidateOnStart();

        services.AddFastEndpoints(options => options.IncludeAbstractValidators = true);

        services.AddMediator(options => options.ServiceLifetime = ServiceLifetime.Scoped);
        services.AddScoped<IAdminTenantGuard, AdminTenantGuard>();
        services.AddScoped<GoogleCalendarCompanyToolResolver>();
        services.AddCors();
        services.AddRateLimiter();
        services.ConfigureOptions<ConfigureCorsOptions>();
        services.ConfigureOptions<ConfigureRateLimiterOptions>();
        services.ConfigureOptions<ConfigureForwardedHeadersOptions>();

        return services;
    }

    public static IApplicationBuilder UseConfiguredCors(this IApplicationBuilder app)
    {
        return app.UseCors(CorsPolicyName);
    }
}
