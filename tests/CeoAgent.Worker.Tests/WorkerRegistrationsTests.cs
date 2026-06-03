using Azure.Storage.Queues;
using CeoAgent.Adapters;
using CeoAgent.Infrastructure.DependencyInjection;
using CeoAgent.Worker.Jobs;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
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
}
