using CeoAgent.Infrastructure;
using Microsoft.AspNetCore.Authentication;
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
    public static readonly Guid DefaultOrganizationId = Guid.Parse("b36cfb51-83bd-4376-b7d7-0502141ff6ae");

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
        builder.UseSetting("Keycloak:ClientId", "ceo-agent-web");
        builder.UseSetting("Keycloak:Issuer", "https://keycloak.test/realms/ceo-agent");
        builder.ConfigureServices(services =>
        {
            services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = TestAuthentication.Scheme;
                options.DefaultChallengeScheme = TestAuthentication.Scheme;
            }).AddScheme<AuthenticationSchemeOptions, TestAuthenticationHandler>(
                TestAuthentication.Scheme,
                options => { });

            _configureServices?.Invoke(services);
        });
    }

    public HttpClient CreateAuthenticatedClient(Guid? organizationId = null)
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Authorization = TestAuthentication.OrganizationBearer(organizationId ?? DefaultOrganizationId);
        return client;
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
