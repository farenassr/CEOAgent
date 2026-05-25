using CeoAgent.Adapters;
using CeoAgent.Infrastructure;
using CeoAgent.Integrations;
using CeoAgent.Tools;
using Shouldly;

namespace CeoAgent.IntegrationTests;

public sealed class FoundationTests
{
    /// <summary>
    /// Verifies that the infrastructure, adapter, integration, and tool assemblies can be loaded.
    /// </summary>
    [Test]
    public void FoundationProjectsAssemblies_AreLoadable()
    {
        var assemblyNames = new[]
        {
            typeof(InfrastructureAssembly).Assembly.GetName().Name,
            typeof(AdaptersAssembly).Assembly.GetName().Name,
            typeof(IntegrationsAssembly).Assembly.GetName().Name,
            typeof(ToolsAssembly).Assembly.GetName().Name,
        };

        assemblyNames.ShouldBe([
            "CeoAgent.Infrastructure",
            "CeoAgent.Adapters",
            "CeoAgent.Integrations",
            "CeoAgent.Tools",
        ]);
    }
}
