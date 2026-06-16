using CeoAgent.Application.Abstractions.Jobs;
using Shouldly;

namespace CeoAgent.Worker.Tests.Jobs;

public sealed class ProcessIncomingMessageJobTests
{
    [Test]
    public void RetryPolicy_AllowsOnlyInitialAttemptPlusOneRetry()
    {
        ProcessIncomingMessageJobRetryPolicy.MaxAttempts.ShouldBe(2);
        ProcessIncomingMessageJobRetryPolicy.MaxRetries.ShouldBe(1);
    }
}
