using CeoAgent.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using DotNet.Testcontainers.Builders;
using Npgsql;
using Testcontainers.PostgreSql;

namespace CeoAgent.ApiService.Tests.Support;

internal sealed class ApiFactory : WebApplicationFactory<Program>
{
    private readonly PostgreSqlContainer _postgres;
    private readonly string _environmentName;
    private readonly Action<IServiceCollection>? _configureServices;

    public ApiFactory(
        string environmentName = "Testing",
        Action<IServiceCollection>? configureServices = null)
    {
        _environmentName = environmentName;
        _configureServices = configureServices;
        _postgres = new PostgreSqlBuilder("postgres:16-alpine")
            .WithWaitStrategy(Wait.ForUnixContainer()
                .UntilCommandIsCompleted("pg_isready", options => options.WithTimeout(TimeSpan.FromMinutes(3))))
            .Build();

        _postgres.StartAsync().GetAwaiter().GetResult();
        WaitForPostgresAsync(_postgres.GetConnectionString()).GetAwaiter().GetResult();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(_environmentName);

        // Point Npgsql at the Testcontainers instance instead of InMemory.
        builder.UseSetting("Persistence:UseInMemoryDatabase", "false");
        builder.UseSetting("ConnectionStrings:CeoAgent", _postgres.GetConnectionString());
        builder.UseSetting("AdminApiKey:Key", "test-admin-key");
        builder.UseSetting("AdminApiKey:CompanyId", Guid.Parse("018f4f70-8b5f-7b4c-9d1a-0f6c1d7a2b30").ToString());
        builder.ConfigureServices(services => _configureServices?.Invoke(services));
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        var host = base.CreateHost(builder);

        // Apply EF migrations so the schema exists before any test runs.
        using var scope = host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CeoAgentDbContext>();
        db.Database.MigrateAsync().GetAwaiter().GetResult();

        return host;
    }

    public override async ValueTask DisposeAsync()
    {
        await _postgres.DisposeAsync();
        await base.DisposeAsync();
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
}
