using CeoAgent.Application.Company.Abstractions;
using CeoAgent.Application.Company.Implementation;
using CeoAgent.Infrastructure;
using CeoAgent.IntegrationTests.Seed;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace CeoAgent.IntegrationTests.Infrastructure;

internal sealed class PostgresTestDatabase : IAsyncDisposable
{
    private readonly PostgreSqlContainer postgres;

    private PostgresTestDatabase(PostgreSqlContainer postgres, CompanyContextAccessor companyContext, CeoAgentDbContext context)
    {
        this.postgres = postgres;
        CompanyContext = companyContext;
        Context = context;
    }

    public CompanyContextAccessor CompanyContext { get; }

    public CeoAgentDbContext Context { get; }

    public static async Task<PostgresTestDatabase> CreateAsync()
    {
        var postgres = new PostgreSqlBuilder("postgres:18-alpine")
            .Build();

        await postgres.StartAsync();

        var companyContext = new CompanyContextAccessor();
        var context = CeoAgentDbContextTestFactory.CreatePostgres(postgres.GetConnectionString(), companyContext);
        if (context.Database.GetMigrations().Any())
        {
            await context.Database.MigrateAsync();
        }
        else
        {
            await context.Database.EnsureCreatedAsync();
        }

        return new PostgresTestDatabase(postgres, companyContext, context);
    }

    public async Task<CompanySeedIds> SeedCompanyGraphAsync(Guid companyId)
    {
        var seed = await CompanySeed.SeedCompanyGraphAsync(Context, companyId, $"channel-{Guid.CreateVersion7()}");
        CompanyContext.SetCompany(companyId);
        return seed;
    }

    public async ValueTask DisposeAsync()
    {
        await Context.DisposeAsync();
        await postgres.DisposeAsync();
    }
}
