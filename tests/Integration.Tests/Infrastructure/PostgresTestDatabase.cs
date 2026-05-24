using CEOAgent.Application.Company;
using CEOAgent.Infrastructure;
using Integration.Tests.Seed;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace Integration.Tests.Infrastructure;

internal sealed class PostgresTestDatabase : IAsyncDisposable
{
    private readonly PostgreSqlContainer postgres;

    private PostgresTestDatabase(PostgreSqlContainer postgres, CompanyContextAccessor companyContext, CEOAgentDbContext context)
    {
        this.postgres = postgres;
        CompanyContext = companyContext;
        Context = context;
    }

    public CompanyContextAccessor CompanyContext { get; }

    public CEOAgentDbContext Context { get; }

    public static async Task<PostgresTestDatabase> CreateAsync()
    {
        var postgres = new PostgreSqlBuilder("postgres:16-alpine")
            .Build();

        await postgres.StartAsync();

        var companyContext = new CompanyContextAccessor();
        var context = CEOAgentDbContextTestFactory.CreatePostgres(postgres.GetConnectionString(), companyContext);
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
