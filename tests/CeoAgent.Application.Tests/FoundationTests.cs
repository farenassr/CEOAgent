using Shouldly;
using CeoAgent.Application.Abstractions.AITools;

namespace CeoAgent.Application.Tests;

public sealed class FoundationTests
{
    /// <summary>
    /// Verifies that the Application project assembly can be loaded by the test host.
    /// </summary>
    [Test]
    public void ApplicationProjectAssembly_IsLoadable()
    {
        typeof(ApplicationAssembly).Assembly.GetName().Name.ShouldBe("CeoAgent.Application");
    }

    [Test]
    public void AgentToolContracts_LiveInApplicationAssembly()
    {
        typeof(IAgentTool).Assembly.GetName().Name.ShouldBe("CeoAgent.Application");
        typeof(IAgentTool<>).Assembly.GetName().Name.ShouldBe("CeoAgent.Application");
        typeof(IAgentToolCatalog).Assembly.GetName().Name.ShouldBe("CeoAgent.Application");
        typeof(IDynamicAgentToolProvider).Assembly.GetName().Name.ShouldBe("CeoAgent.Application");
        typeof(IAgentToolInvoker).Assembly.GetName().Name.ShouldBe("CeoAgent.Application");
    }
}
