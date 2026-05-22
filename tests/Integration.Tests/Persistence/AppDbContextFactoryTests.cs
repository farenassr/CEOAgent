using CEOAgent.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Shouldly;

namespace Integration.Tests.Persistence;

[NotInParallel]
public sealed class AppDbContextFactoryTests
{
    [Test]
    public void CreateDbContext_WhenConnectionStringMissing_ThrowsClearError()
    {
        using var environment = ConnectionStringEnvironment.Override(null);
        var factory = new AppDbContextFactory();

        var exception = Should.Throw<InvalidOperationException>(() => factory.CreateDbContext([]));

        exception.Message.ShouldContain("ConnectionStrings:CEOAgent");
        exception.Message.ShouldContain("dotnet user-secrets set");
    }

    [Test]
    public void CreateDbContext_WhenConnectionStringConfigured_UsesConfiguredConnectionString()
    {
        const string connectionString = "Host=localhost;Database=ceoagent_test;Username=postgres;Password=test";
        using var environment = ConnectionStringEnvironment.Override(connectionString);
        var factory = new AppDbContextFactory();

        using var dbContext = factory.CreateDbContext([]);

        dbContext.Database.GetConnectionString().ShouldBe(connectionString);
    }

    private sealed class ConnectionStringEnvironment : IDisposable
    {
        private const string Key = "ConnectionStrings__CEOAgent";
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
