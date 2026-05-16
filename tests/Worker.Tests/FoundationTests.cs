using Shouldly;

namespace Worker.Tests;

public sealed class FoundationTests
{
    [Test]
    public void WorkerProjectAssembly_IsLoadable()
    {
        typeof(CEOAgent.Worker.Worker).Assembly.GetName().Name.ShouldBe("CEOAgent.Worker");
    }
}
