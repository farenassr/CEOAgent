using CEOAgent.Application.Company;
using CEOAgent.Infrastructure.Persistence;
using CEOAgent.Infrastructure.Tools;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CEOAgent.Infrastructure;

public static class InfrastructureServiceRegistrations
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddSingleton<ICompanyContextAccessor, CompanyContextAccessor>();
        services.AddSingleton<ICompanyContext>(provider => provider.GetRequiredService<ICompanyContextAccessor>());
        services.AddSingleton(TimeProvider.System);
        services.AddScoped<ToolExecutionGateway>();

        services.AddDbContext<AppDbContext>(options =>
        {
            if (bool.TryParse(configuration["Persistence:UseInMemoryDatabase"], out var useInMemoryDatabase)
                && useInMemoryDatabase)
            {
                options.UseInMemoryDatabase(configuration["Persistence:InMemoryDatabaseName"] ?? "CEOAgent");
                return;
            }

            var connectionString = configuration.GetConnectionString("CEOAgent")
                ?? configuration.GetConnectionString("DefaultConnection")
                ?? "Host=localhost;Database=CEOAgent;Username=postgres;Password=postgres";

            options.UseNpgsql(connectionString)
                .UseSnakeCaseNamingConvention();
        });

        return services;
    }
}
