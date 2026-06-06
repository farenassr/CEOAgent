using CeoAgent.Application.Abstractions.Company;
using CeoAgent.Infrastructure.Implementation.Company;
using CeoAgent.Infrastructure;
using CeoAgent.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace CeoAgent.Worker.Tests.Infrastructure;

internal sealed class PostgresWorkerDatabase : IAsyncDisposable
{
    private readonly PostgreSqlContainer postgres;

    private PostgresWorkerDatabase(PostgreSqlContainer postgres, CompanyContextAccessor companyContext, CeoAgentDbContext context)
    {
        this.postgres = postgres;
        CompanyContext = companyContext;
        Context = context;
    }

    public CompanyContextAccessor CompanyContext { get; }

    public CeoAgentDbContext Context { get; }

    public static async Task<PostgresWorkerDatabase> CreateAsync()
    {
        var postgres = new PostgreSqlBuilder("postgres:16-alpine").Build();
        await postgres.StartAsync();

        var companyContext = new CompanyContextAccessor();
        var options = new DbContextOptionsBuilder<CeoAgentDbContext>()
            .UseNpgsql(postgres.GetConnectionString())
            .UseSnakeCaseNamingConvention()
            .AddInterceptors(new ConversationAgentProfileImmutabilityInterceptor())
            .Options;
        var context = new CeoAgentDbContext(options, companyContext, TimeProvider.System);
        await context.Database.MigrateAsync();

        return new PostgresWorkerDatabase(postgres, companyContext, context);
    }

    public async ValueTask DisposeAsync()
    {
        await Context.DisposeAsync();
        await postgres.DisposeAsync();
    }
}
