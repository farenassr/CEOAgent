using CeoAgent.Application.Abstractions.Company;
using CeoAgent.Infrastructure.Implementation.Company;
using CeoAgent.Infrastructure;
using CeoAgent.IntegrationTests.Seed;
using Microsoft.EntityFrameworkCore;
using DotNet.Testcontainers.Builders;
using Npgsql;
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
            .WithWaitStrategy(Wait.ForUnixContainer()
                .UntilCommandIsCompleted("pg_isready", options => options.WithTimeout(TimeSpan.FromMinutes(3))))
            .Build();

        await postgres.StartAsync();
        await WaitForPostgresAsync(postgres.GetConnectionString());

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

    private static async Task WaitForPostgresAsync(string connectionString)
    {
        var deadline = TimeProvider.System.GetUtcNow().AddMinutes(3);
        Exception? lastError = null;

        while (TimeProvider.System.GetUtcNow() < deadline)
        {
            try
            {
                await using var connection = new NpgsqlConnection(connectionString);
                await connection.OpenAsync();
                await using var command = new NpgsqlCommand("SELECT 1", connection);
                await command.ExecuteScalarAsync();
                return;
            }
            catch (Exception ex) when (ex is NpgsqlException or TimeoutException or IOException)
            {
                lastError = ex;
                await Task.Delay(TimeSpan.FromSeconds(1));
            }
        }

        throw new TimeoutException("PostgreSQL test container did not accept connections before the timeout.", lastError);
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
