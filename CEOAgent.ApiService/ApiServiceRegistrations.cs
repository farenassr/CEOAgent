using CEOAgent.ApiService.Infrastructure.Auth;
using FastEndpoints;
using Mediator;

namespace CEOAgent.ApiService;

public static class ApiServiceRegistrations
{
    public static IServiceCollection AddApiService(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddFastEndpoints(options => options.IncludeAbstractValidators = true);

        services.AddAuthentication(AdminApiKeyAuthenticationDefaults.AuthenticationScheme)
            .AddScheme<AdminApiKeyOptions, AdminApiKeyAuthenticationHandler>(
                AdminApiKeyAuthenticationDefaults.AuthenticationScheme,
                options => options.ApiKey = configuration["Authentication:AdminApiKey"]);

        services.AddAuthorization();
        services.AddMediator(options => options.ServiceLifetime = ServiceLifetime.Scoped);

        return services;
    }
}
