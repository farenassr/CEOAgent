using CeoAgent.Application.Abstractions.Organization;
using CeoAgent.Infrastructure.Implementation.Organization;
using CeoAgent.Infrastructure;
using CeoAgent.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using DotNet.Testcontainers.Builders;
using Npgsql;
using Testcontainers.PostgreSql;

namespace CeoAgent.ApiService.Tests.Support;

internal sealed class PostgresApiDatabase : IAsyncDisposable
{
    private readonly PostgreSqlContainer postgres;

    private PostgresApiDatabase(PostgreSqlContainer postgres, OrganizationContextAccessor companyContext, CeoAgentDbContext context)
    {
        this.postgres = postgres;
        OrganizationContext = companyContext;
        Context = context;
    }

    public OrganizationContextAccessor OrganizationContext { get; }

    public CeoAgentDbContext Context { get; }

    public static async Task<PostgresApiDatabase> CreateAsync()
    {
        var postgres = new PostgreSqlBuilder("postgres:16-alpine")
            .WithWaitStrategy(Wait.ForUnixContainer()
                .UntilCommandIsCompleted("pg_isready", options => options.WithTimeout(TimeSpan.FromMinutes(3))))
            .Build();
        await postgres.StartAsync();
        await WaitForPostgresAsync(postgres.GetConnectionString());

        var companyContext = new OrganizationContextAccessor();
        var options = new DbContextOptionsBuilder<CeoAgentDbContext>()
            .UseNpgsql(postgres.GetConnectionString())
            .UseSnakeCaseNamingConvention()
            .AddInterceptors(new ConversationAgentProfileImmutabilityInterceptor())
            .Options;
        var context = new CeoAgentDbContext(options, companyContext, TimeProvider.System);
        await context.Database.MigrateAsync();

        return new PostgresApiDatabase(postgres, companyContext, context);
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

    public async ValueTask DisposeAsync()
    {
        await Context.DisposeAsync();
        await postgres.DisposeAsync();
    }
}
