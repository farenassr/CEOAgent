using CeoAgent.Application.Company;
using CeoAgent.Infrastructure.DependencyInjection;
using CeoAgent.Infrastructure.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Shouldly;

namespace CeoAgent.ApiService.Tests.Persistence;

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

    /// <summary>
    /// Verifies that persistence options reject invalid startup configuration.
    /// </summary>
    [Test]
    public void AddInfrastructure_WhenInMemoryDatabaseNameIsMissing_FailsOptionsValidation()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Persistence:UseInMemoryDatabase"] = "true",
                ["Persistence:InMemoryDatabaseName"] = string.Empty,
            })
            .Build();
        var services = new ServiceCollection();

        services.AddInfrastructure(configuration);

        using var provider = services.BuildServiceProvider();
        var action = () => provider.GetRequiredService<IOptions<PersistenceOptions>>().Value;

        action.ShouldThrow<OptionsValidationException>();
    }

    /// <summary>
    /// Verifies that company context state is isolated between dependency injection scopes.
    /// </summary>
    [Test]
    public void AddInfrastructure_RegistersCompanyContextStatePerScope()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Persistence:UseInMemoryDatabase"] = "true",
                ["Persistence:InMemoryDatabaseName"] = "company-context-scope-test-db",
            })
            .Build();
        var services = new ServiceCollection();
        var companyId = Guid.CreateVersion7();

        services.AddInfrastructure(configuration);

        using var provider = services.BuildServiceProvider();
        using var firstScope = provider.CreateScope();
        using var secondScope = provider.CreateScope();
        var firstAccessor = firstScope.ServiceProvider.GetRequiredService<ICompanyContextAccessor>();
        var secondContext = secondScope.ServiceProvider.GetRequiredService<ICompanyContext>();

        firstAccessor.SetCompany(companyId);

        firstAccessor.CompanyId.ShouldBe(companyId);
        secondContext.CompanyId.ShouldBeNull();
    }
}
