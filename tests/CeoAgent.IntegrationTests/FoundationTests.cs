using CeoAgent.Infrastructure;
using CeoAgent.Application;
using CeoAgent.Shared.Constants;
using Shouldly;

namespace CeoAgent.IntegrationTests;

public sealed class FoundationTests
{
    /// <summary>
    /// Verifies that the foundation assemblies can be loaded.
    /// </summary>
    [Test]
    public void FoundationProjectsAssemblies_AreLoadable()
    {
        var assemblyNames = new[]
        {
            typeof(InfrastructureAssembly).Assembly.GetName().Name,
            typeof(ApplicationAssembly).Assembly.GetName().Name,
            typeof(MvpToolKeys).Assembly.GetName().Name,
        };

        assemblyNames.ShouldBe([
            "CeoAgent.Infrastructure",
            "CeoAgent.Application",
            "CeoAgent.Shared",
        ]);
    }
}
