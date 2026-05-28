using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Queues;
using Azure.Storage.Queues.Models;
using CeoAgent.Infrastructure.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using NSubstitute;
using Shouldly;

namespace CeoAgent.IntegrationTests.Infrastructure;

public sealed class AzureStorageHealthCheckTests
{
    [Test]
    public void AddAzureStorageMetadataHealthChecks_RegistersQueueAndBlobMetadataChecks()
    {
        var services = new ServiceCollection();

        services.AddAzureStorageMetadataHealthChecks();

        using var serviceProvider = services.BuildServiceProvider();
        var options = serviceProvider.GetRequiredService<IOptions<HealthCheckServiceOptions>>().Value;
        var names = options.Registrations.Select(registration => registration.Name).ToArray();

        names.ShouldContain(AzureStorageHealthCheckNames.Queues);
        names.ShouldContain(AzureStorageHealthCheckNames.Blobs);
    }

    [Test]
    public async Task AzureQueueServiceMetadataHealthCheck_ReturnsHealthyWhenMetadataIsReadable()
    {
        var queueServiceClient = Substitute.For<QueueServiceClient>();
        var response = Response.FromValue(default(QueueServiceProperties)!, Substitute.For<Response>());
        queueServiceClient
            .GetPropertiesAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(response));
        var healthCheck = new AzureQueueServiceMetadataHealthCheck(queueServiceClient);

        var result = await healthCheck.CheckHealthAsync(new HealthCheckContext());

        result.Status.ShouldBe(HealthStatus.Healthy);
    }

    [Test]
    public async Task AzureBlobServiceMetadataHealthCheck_ReturnsHealthyWhenMetadataIsReadable()
    {
        var blobServiceClient = Substitute.For<BlobServiceClient>();
        var response = Response.FromValue(default(BlobServiceProperties)!, Substitute.For<Response>());
        blobServiceClient
            .GetPropertiesAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(response));
        var healthCheck = new AzureBlobServiceMetadataHealthCheck(blobServiceClient);

        var result = await healthCheck.CheckHealthAsync(new HealthCheckContext());

        result.Status.ShouldBe(HealthStatus.Healthy);
    }
}
