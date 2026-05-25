using Shouldly;

namespace CeoAgent.Worker.Tests;

public sealed class FoundationTests
{
    /// <summary>
    /// Verifies that the Worker project assembly can be loaded by the test host.
    /// </summary>
    [Test]
    public void WorkerProjectAssembly_IsLoadable()
    {
        typeof(CeoAgent.Worker.Worker).Assembly.GetName().Name.ShouldBe("CeoAgent.Worker");
    }
}
