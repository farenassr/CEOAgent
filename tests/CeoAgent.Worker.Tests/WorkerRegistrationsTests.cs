using Azure.Storage.Queues;
using CeoAgent.Adapters;
using CeoAgent.Infrastructure.DependencyInjection;
using CeoAgent.Worker.Jobs;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Shouldly;

namespace CeoAgent.Worker.Tests;

public sealed class WorkerRegistrationsTests
{
    [Test]
    public void AddWorkerRuntime_WithAdapters_ConstructsIncomingMessageProcessor()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Persistence:UseInMemoryDatabase"] = "true",
                ["Persistence:InMemoryDatabaseName"] = $"worker-di-{Guid.CreateVersion7()}",
            })
            .Build();
        var services = new ServiceCollection();

        services.AddLogging(builder => builder.AddDebug());
        services.AddSingleton(new QueueServiceClient("UseDevelopmentStorage=true"));
        services.AddInfrastructure(configuration);
        services.AddAdapters(configuration);
        services.AddWorkerRuntime();

        services.ShouldContain(descriptor =>
            descriptor.ServiceType == typeof(IHostedService)
            && descriptor.ImplementationType == typeof(IncomingMessageQueueWorker));

        using var provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true,
        });
        using var scope = provider.CreateScope();

        var processor = scope.ServiceProvider.GetRequiredService<ProcessIncomingMessageJobProcessor>();

        processor.ShouldNotBeNull();
    }

    [Test]
    public void AddWorkerRuntime_WhenIncomingQueueBatchSizeExceedsAzureLimit_FailsOptionsValidation()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["IncomingQueue:MaxMessages"] = "33",
                ["IncomingQueue:MaxDegreeOfParallelism"] = "4",
                ["IncomingQueue:VisibilityTimeoutMinutes"] = "5",
                ["IncomingQueue:EmptyQueueDelayMilliseconds"] = "1000",
            })
            .Build();
        var services = new ServiceCollection();

        services.AddWorkerRuntime();
        services.AddSingleton<IConfiguration>(configuration);

        using var provider = services.BuildServiceProvider();

        var action = () => provider.GetRequiredService<IOptions<IncomingQueueOptions>>().Value;

        action.ShouldThrow<OptionsValidationException>();
    }

    [Test]
    public void WorkerHealthTracker_UsesInjectedTimeProviderForStalePollDetection()
    {
        var timeProvider = new ManualTimeProvider(new DateTimeOffset(2026, 6, 4, 12, 0, 0, TimeSpan.Zero));
        var tracker = new WorkerHealthTracker(timeProvider);

        tracker.RecordPoll();
        timeProvider.Advance(TimeSpan.FromMinutes(3));

        tracker.IsHealthy(TimeSpan.FromMinutes(2)).ShouldBeFalse();
    }

    private sealed class ManualTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        private DateTimeOffset utcNow = utcNow;

        public override DateTimeOffset GetUtcNow()
        {
            return utcNow;
        }

        public void Advance(TimeSpan duration)
        {
            utcNow += duration;
        }
    }
}
