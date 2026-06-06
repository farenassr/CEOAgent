using CeoAgent.Worker.Jobs;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CeoAgent.Worker;

public static class WorkerRegistrations
{
    public static IServiceCollection AddWorkerRuntime(this IServiceCollection services)
    {
        services.AddOptions<IncomingQueueOptions>()
            .BindConfiguration(IncomingQueueOptions.SectionName)
            .Validate(
                options => options.MaxMessages is >= 1 and <= 32,
                "IncomingQueue:MaxMessages must be between 1 and 32.")
            .Validate(
                options => options.MaxDegreeOfParallelism is >= 1 and <= 32,
                "IncomingQueue:MaxDegreeOfParallelism must be between 1 and 32.")
            .Validate(
                options => options.VisibilityTimeoutMinutes > 0,
                "IncomingQueue:VisibilityTimeoutMinutes must be greater than 0.")
            .Validate(
                options => options.EmptyQueueDelayMilliseconds > 0,
                "IncomingQueue:EmptyQueueDelayMilliseconds must be greater than 0.")
            .ValidateOnStart();

        services.TryAddSingleton(TimeProvider.System);
        services.AddScoped<ProcessIncomingMessageJobProcessor>();

        services.AddSingleton<WorkerHealthTracker>();
        services.AddHealthChecks()
            .AddCheck<IncomingMessageQueueWorkerHealthCheck>("IncomingMessageQueueWorker", tags: ["live"]);

        services.AddHostedService<IncomingMessageQueueWorker>();

        return services;
    }
}
