using Microsoft.Extensions.Diagnostics.HealthChecks;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace CeoAgent.Worker.Jobs;

public sealed class IncomingMessageQueueWorkerHealthCheck(WorkerHealthTracker tracker) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        if (tracker.IsHealthy(TimeSpan.FromMinutes(2)))
        {
            return Task.FromResult(HealthCheckResult.Healthy("Worker loop is polling active."));
        }

        return Task.FromResult(HealthCheckResult.Unhealthy("Worker loop has not completed a queue poll recently."));
    }
}
