using CeoAgent.Application.Abstractions.Organization;
using CeoAgent.Infrastructure.Implementation.Organization;
using CeoAgent.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace CeoAgent.Infrastructure;

public sealed class CeoAgentDbContextFactory : IDesignTimeDbContextFactory<CeoAgentDbContext>
{
    public CeoAgentDbContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddUserSecrets<CeoAgentDbContextFactory>(optional: true)
            .AddEnvironmentVariables()
            .Build();

        var connectionString = configuration.GetConnectionString("CeoAgent");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "The design-time connection string 'ConnectionStrings:CeoAgent' is not configured. " +
                "For local EF commands, set it with: dotnet user-secrets set \"ConnectionStrings:CeoAgent\" \"<postgres-connection-string>\" --project CeoAgent.Infrastructure");
        }

        var options = new DbContextOptionsBuilder<CeoAgentDbContext>()
            .UseNpgsql(connectionString)
            .UseSnakeCaseNamingConvention()
            .AddInterceptors(new ConversationAgentProfileImmutabilityInterceptor())
            .Options;

        return new CeoAgentDbContext(options, new OrganizationContextAccessor(), TimeProvider.System);
    }
}
