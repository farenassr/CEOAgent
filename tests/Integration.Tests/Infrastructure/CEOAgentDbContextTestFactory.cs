using CEOAgent.Application.Company;
using CEOAgent.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Integration.Tests.Infrastructure;

internal static class CEOAgentDbContextTestFactory
{
    public static CEOAgentDbContext CreatePostgres(string connectionString, ICompanyContext companyContext)
    {
        var options = new DbContextOptionsBuilder<CEOAgentDbContext>()
            .UseNpgsql(connectionString)
            .UseSnakeCaseNamingConvention()
            .Options;

        return new CEOAgentDbContext(options, companyContext, TimeProvider.System);
    }

    public static CEOAgentDbContext CreateInMemory(ICompanyContext companyContext, string? databaseName = null)
    {
        var options = new DbContextOptionsBuilder<CEOAgentDbContext>()
            .UseInMemoryDatabase(databaseName ?? $"ceoagent-tests-{Guid.CreateVersion7()}")
            .Options;

        return new CEOAgentDbContext(options, companyContext, TimeProvider.System);
    }
}
