using CeoAgent.Integrations.Jobs;
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

    [Test]
    public void Contract_CarriesCompanyConversationAndMessageIdentifiers()
    {
        var job = new ProcessIncomingMessageJob(
            Guid.Parse("018f4f70-8b5f-7b4c-9d1a-0f6c1d7a2b30"),
            Guid.Parse("018f4f70-8b5f-7b4c-9d1a-0f6c1d7a2b34"),
            Guid.Parse("018f4f70-8b5f-7b4c-9d1a-0f6c1d7a2b36"),
            "correlation-123");

        job.CompanyId.ShouldBe(Guid.Parse("018f4f70-8b5f-7b4c-9d1a-0f6c1d7a2b30"));
        job.ConversationId.ShouldBe(Guid.Parse("018f4f70-8b5f-7b4c-9d1a-0f6c1d7a2b34"));
        job.MessageId.ShouldBe(Guid.Parse("018f4f70-8b5f-7b4c-9d1a-0f6c1d7a2b36"));
        job.CorrelationId.ShouldBe("correlation-123");
        job.JobId.ShouldNotBe(Guid.Empty);
    }
}
