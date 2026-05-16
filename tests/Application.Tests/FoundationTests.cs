using CEOAgent.Application;
using Shouldly;

namespace Application.Tests;

public sealed class FoundationTests
{
    [Test]
    public void ApplicationProjectAssembly_IsLoadable()
    {
        typeof(ApplicationAssembly).Assembly.GetName().Name.ShouldBe("CEOAgent.Application");
    }
}
