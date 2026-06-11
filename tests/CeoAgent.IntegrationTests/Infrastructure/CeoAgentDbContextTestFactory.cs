using CeoAgent.Application.Abstractions.Organization;
using CeoAgent.Infrastructure.Implementation.Organization;
using CeoAgent.Infrastructure;
using CeoAgent.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CeoAgent.IntegrationTests.Infrastructure;

internal static class CeoAgentDbContextTestFactory
{
    public static CeoAgentDbContext CreatePostgres(string connectionString, IOrganizationContextProvider companyContext)
    {
        var options = new DbContextOptionsBuilder<CeoAgentDbContext>()
            .UseNpgsql(connectionString)
            .UseSnakeCaseNamingConvention()
            .AddInterceptors(new ConversationAgentProfileImmutabilityInterceptor())
            .Options;

        return new CeoAgentDbContext(options, companyContext, TimeProvider.System);
    }

    public static CeoAgentDbContext CreateInMemory(IOrganizationContextProvider companyContext, string? databaseName = null)
    {
        var options = new DbContextOptionsBuilder<CeoAgentDbContext>()
            .UseInMemoryDatabase(databaseName ?? $"CeoAgent-tests-{Guid.CreateVersion7()}")
            .AddInterceptors(new ConversationAgentProfileImmutabilityInterceptor())
            .Options;

        return new CeoAgentDbContext(options, companyContext, TimeProvider.System);
    }
}
