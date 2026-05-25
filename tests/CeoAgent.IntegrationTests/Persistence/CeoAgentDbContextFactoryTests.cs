using CeoAgent.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Shouldly;

namespace CeoAgent.IntegrationTests.Persistence;

[NotInParallel]
public sealed class CeoAgentDbContextFactoryTests
{
    /// <summary>
    /// Verifies that the design-time DbContext factory fails with a clear setup message when no connection string is configured.
    /// </summary>
    [Test]
    public void CreateDbContext_WhenConnectionStringMissing_ThrowsClearError()
    {
        using var environment = ConnectionStringEnvironment.Override(string.Empty);
        var factory = new CeoAgentDbContextFactory();

        var exception = Should.Throw<InvalidOperationException>(() => factory.CreateDbContext([]));

        exception.Message.ShouldContain("ConnectionStrings:CeoAgent");
        exception.Message.ShouldContain("dotnet user-secrets set");
    }

    /// <summary>
    /// Verifies that the design-time DbContext factory uses the configured CeoAgent connection string.
    /// </summary>
    [Test]
    public void CreateDbContext_WhenConnectionStringConfigured_UsesConfiguredConnectionString()
    {
        const string connectionString = "Host=localhost;Database=CeoAgent_test;Username=postgres;Password=test";
        using var environment = ConnectionStringEnvironment.Override(connectionString);
        var factory = new CeoAgentDbContextFactory();

        using var dbContext = factory.CreateDbContext([]);

        dbContext.Database.GetConnectionString().ShouldBe(connectionString);
    }

    private sealed class ConnectionStringEnvironment : IDisposable
    {
        private const string Key = "ConnectionStrings__CeoAgent";
        private readonly string? originalValue;

        private ConnectionStringEnvironment(string? value)
        {
            originalValue = Environment.GetEnvironmentVariable(Key);
            Environment.SetEnvironmentVariable(Key, value);
        }

        public static ConnectionStringEnvironment Override(string? value)
        {
            return new ConnectionStringEnvironment(value);
        }

        public void Dispose()
        {
            Environment.SetEnvironmentVariable(Key, originalValue);
        }
    }
}
