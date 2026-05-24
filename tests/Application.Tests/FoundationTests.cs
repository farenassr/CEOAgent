using CEOAgent.Application;
using Shouldly;

namespace Application.Tests;

public sealed class FoundationTests
{
    /// <summary>
    /// Verifies that the Application project assembly can be loaded by the test host.
    /// </summary>
    [Test]
    public void ApplicationProjectAssembly_IsLoadable()
    {
        typeof(ApplicationAssembly).Assembly.GetName().Name.ShouldBe("CEOAgent.Application");
    }
}
