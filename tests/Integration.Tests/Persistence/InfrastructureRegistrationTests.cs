using CEOAgent.Infrastructure.DependencyInjection;
using CEOAgent.Infrastructure.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Shouldly;

namespace CEOAgent.Tests.Persistence;

public sealed class InfrastructureRegistrationTests
{
    /// <summary>
    /// Verifies that infrastructure registration exposes persistence options through dependency injection.
    /// </summary>
    [Test]
    public void AddInfrastructure_RegistersPersistenceOptionsForDependencyInjection()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Persistence:UseInMemoryDatabase"] = "true",
                ["Persistence:InMemoryDatabaseName"] = "options-test-db",
            })
            .Build();
        var services = new ServiceCollection();

        services.AddInfrastructure(configuration);

        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<PersistenceOptions>>().Value;
        var directOptions = provider.GetRequiredService<PersistenceOptions>();

        options.UseInMemoryDatabase.ShouldBeTrue();
        options.InMemoryDatabaseName.ShouldBe("options-test-db");
        directOptions.ShouldBeSameAs(options);
    }
}
