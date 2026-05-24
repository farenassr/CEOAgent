using CEOAgent.Application.Company;
using CEOAgent.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace CEOAgent.Infrastructure.DependencyInjection;

public static class InfrastructureRegistrations
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddOptions<PersistenceOptions>()
            .Bind(configuration.GetSection("Persistence"))
            .Validate(options => !options.UseInMemoryDatabase || !string.IsNullOrWhiteSpace(options.InMemoryDatabaseName),
                "Persistence:InMemoryDatabaseName is required when Persistence:UseInMemoryDatabase is true.");
        services.AddSingleton(provider => provider.GetRequiredService<IOptions<PersistenceOptions>>().Value);

        services.AddSingleton<ICompanyContextAccessor, CompanyContextAccessor>();
        services.AddSingleton<ICompanyContext>(provider => provider.GetRequiredService<ICompanyContextAccessor>());
        services.AddSingleton(TimeProvider.System);

        var persistenceOptions = configuration.GetSection("Persistence").Get<PersistenceOptions>() ?? new PersistenceOptions();

        services.AddDbContextPool<CEOAgentDbContext>(options =>
        {
            if (persistenceOptions.UseInMemoryDatabase)
            {
                options.UseInMemoryDatabase(persistenceOptions.InMemoryDatabaseName);
                return;
            }

            var connectionString = configuration.GetConnectionString("CEOAgent") ?? configuration.GetConnectionString("DefaultConnection");

            options.UseNpgsql(connectionString)
                .UseSnakeCaseNamingConvention();
        });

        return services;
    }
}
