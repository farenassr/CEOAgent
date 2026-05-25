using System.Diagnostics;
using CeoAgent.ApiService.Infrastructure.Correlation;
using CeoAgent.ApiService.Infrastructure.ErrorHandling;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;

namespace CeoAgent.ApiService.Tests;

public sealed class GlobalExceptionHandlerTests
{
    [Test]
    public async Task TryHandleAsync_DoesNotRecordExceptionEventOnCurrentActivity()
    {
        var handler = new GlobalExceptionHandler(
            new NoopProblemDetailsService(),
            new CorrelationIdAccessor(),
            NullLogger<GlobalExceptionHandler>.Instance);

        using var activity = new Activity("test-request");
        activity.Start();

        try
        {
            await handler.TryHandleAsync(
                new DefaultHttpContext(),
                new InvalidOperationException("internal path C:\\Users\\siemp\\source"),
                CancellationToken.None);
        }
        finally
        {
            activity.Stop();
        }

        activity.Events.ShouldBeEmpty();
    }

    private sealed class NoopProblemDetailsService : IProblemDetailsService
    {
        public ValueTask WriteAsync(ProblemDetailsContext context)
        {
            return ValueTask.CompletedTask;
        }
    }
}
