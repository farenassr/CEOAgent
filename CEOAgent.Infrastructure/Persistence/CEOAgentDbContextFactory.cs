using CEOAgent.Application.Company;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace CEOAgent.Infrastructure.Persistence;

public sealed class CEOAgentDbContextFactory : IDesignTimeDbContextFactory<CEOAgentDbContext>
{
    public CEOAgentDbContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddUserSecrets<CEOAgentDbContextFactory>(optional: true)
            .AddEnvironmentVariables()
            .Build();

        var connectionString = configuration.GetConnectionString("CEOAgent");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "The design-time connection string 'ConnectionStrings:CEOAgent' is not configured. " +
                "For local EF commands, set it with: dotnet user-secrets set \"ConnectionStrings:CEOAgent\" \"<postgres-connection-string>\" --project CEOAgent.Infrastructure");
        }

        var options = new DbContextOptionsBuilder<CEOAgentDbContext>()
            .UseNpgsql(connectionString)
            .UseSnakeCaseNamingConvention()
            .Options;

        return new CEOAgentDbContext(options, new CompanyContextAccessor(), TimeProvider.System);
    }
}
