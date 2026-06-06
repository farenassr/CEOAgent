using CeoAgent.Application.Abstractions.Company;
using CeoAgent.Infrastructure.Implementation.Company;
using CeoAgent.Infrastructure;
using CeoAgent.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CeoAgent.IntegrationTests.Infrastructure;

internal static class CeoAgentDbContextTestFactory
{
    public static CeoAgentDbContext CreatePostgres(string connectionString, ICompanyContext companyContext)
    {
        var options = new DbContextOptionsBuilder<CeoAgentDbContext>()
            .UseNpgsql(connectionString)
            .UseSnakeCaseNamingConvention()
            .AddInterceptors(new ConversationAgentProfileImmutabilityInterceptor())
            .Options;

        return new CeoAgentDbContext(options, companyContext, TimeProvider.System);
    }

    public static CeoAgentDbContext CreateInMemory(ICompanyContext companyContext, string? databaseName = null)
    {
        var options = new DbContextOptionsBuilder<CeoAgentDbContext>()
            .UseInMemoryDatabase(databaseName ?? $"CeoAgent-tests-{Guid.CreateVersion7()}")
            .AddInterceptors(new ConversationAgentProfileImmutabilityInterceptor())
            .Options;

        return new CeoAgentDbContext(options, companyContext, TimeProvider.System);
    }
}
