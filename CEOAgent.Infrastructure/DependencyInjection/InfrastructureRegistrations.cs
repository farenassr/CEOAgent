using CeoAgent.Application.Company;
using CeoAgent.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace CeoAgent.Infrastructure.DependencyInjection;

public static class InfrastructureRegistrations
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.TryAddSingleton(configuration);
        services.AddOptions<PersistenceOptions>()
            .BindConfiguration(PersistenceOptions.SectionName)
            .Validate(PersistenceOptions.IsValid,
                "Persistence:InMemoryDatabaseName is required when Persistence:UseInMemoryDatabase is true.")
            .ValidateOnStart();
        services.AddSingleton(provider => provider.GetRequiredService<IOptions<PersistenceOptions>>().Value);

        services.AddScoped<ICompanyContextAccessor, CompanyContextAccessor>();
        services.AddScoped<ICompanyContext>(provider => provider.GetRequiredService<ICompanyContextAccessor>());
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<ConversationAgentProfileImmutabilityInterceptor>();

        services.AddDbContext<CeoAgentDbContext>((provider, options) =>
        {
            var persistenceOptions = provider.GetRequiredService<IOptions<PersistenceOptions>>().Value;
            options.AddInterceptors(provider.GetRequiredService<ConversationAgentProfileImmutabilityInterceptor>());

            if (persistenceOptions.UseInMemoryDatabase)
            {
                options.UseInMemoryDatabase(persistenceOptions.InMemoryDatabaseName);
                return;
            }

            var configuration = provider.GetRequiredService<IConfiguration>();
            var connectionString = configuration.GetConnectionString("CeoAgent") ?? configuration.GetConnectionString("DefaultConnection");

            options.UseNpgsql(connectionString)
                .UseSnakeCaseNamingConvention();
        });

        return services;
    }
}
