using CEOAgent.Adapters;
using CEOAgent.Infrastructure;
using CEOAgent.Integrations;
using CEOAgent.Tools;
using Shouldly;

namespace Integration.Tests;

public sealed class FoundationTests
{
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
            "CEOAgent.Infrastructure",
            "CEOAgent.Adapters",
            "CEOAgent.Integrations",
            "CEOAgent.Tools",
        ]);
    }
}
