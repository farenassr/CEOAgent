using CeoAgent.Tools;
using CeoAgent.Worker.Jobs;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CeoAgent.Worker;

public static class WorkerRegistrations
{
    public static IServiceCollection AddWorkerRuntime(this IServiceCollection services)
    {
        services.AddOptions<IncomingQueueOptions>()
            .BindConfiguration(IncomingQueueOptions.SectionName);

        services.AddToolsRuntime();
        services.TryAddScoped<IAudioBlobStore, UnavailableAudioBlobStore>();
        services.AddScoped<ProcessIncomingMessageJobProcessor>();
        services.AddHostedService<IncomingMessageQueueWorker>();

        return services;
    }
}
