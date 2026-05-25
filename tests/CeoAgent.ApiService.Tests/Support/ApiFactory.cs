using CeoAgent.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Testcontainers.PostgreSql;

namespace CeoAgent.ApiService.Tests.Support;

internal sealed class ApiFactory : WebApplicationFactory<Program>
{
    private readonly PostgreSqlContainer _postgres;
    private readonly string _environmentName;

    public ApiFactory(string environmentName = "Testing")
    {
        _environmentName = environmentName;
        _postgres = new PostgreSqlBuilder("postgres:16-alpine")
            .Build();

        _postgres.StartAsync().GetAwaiter().GetResult();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(_environmentName);

        // Point Npgsql at the Testcontainers instance instead of InMemory.
        builder.UseSetting("Persistence:UseInMemoryDatabase", "false");
        builder.UseSetting("ConnectionStrings:CeoAgent", _postgres.GetConnectionString());
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
}
