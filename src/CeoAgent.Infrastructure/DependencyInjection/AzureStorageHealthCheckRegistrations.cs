using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Queues;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace CeoAgent.Infrastructure.DependencyInjection;

public static class AzureStorageHealthCheckNames
{
    public const string Queues = "azure_storage_queues";
    public const string Blobs = "azure_storage_blobs";
}

public static class AzureStorageHealthCheckRegistrations
{
    public static IServiceCollection AddAzureStorageMetadataHealthChecks(this IServiceCollection services)
    {
        services.AddAzureQueueServiceMetadataHealthCheck();
        services.AddAzureBlobServiceMetadataHealthCheck();

        return services;
    }

    public static IServiceCollection AddAzureQueueServiceMetadataHealthCheck(this IServiceCollection services)
    {
        services
            .AddHealthChecks()
            .AddCheck<AzureQueueServiceMetadataHealthCheck>(
                AzureStorageHealthCheckNames.Queues,
                failureStatus: HealthStatus.Unhealthy);

        return services;
    }

    public static IServiceCollection AddAzureBlobServiceMetadataHealthCheck(this IServiceCollection services)
    {
        services
            .AddHealthChecks()
            .AddCheck<AzureBlobServiceMetadataHealthCheck>(
                AzureStorageHealthCheckNames.Blobs,
                failureStatus: HealthStatus.Unhealthy);

        return services;
    }
}

public sealed class AzureQueueServiceMetadataHealthCheck(QueueServiceClient queueServiceClient) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await queueServiceClient.GetPropertiesAsync(cancellationToken);
            return HealthCheckResult.Healthy("Azure Storage Queue service metadata is readable.");
        }
        catch (RequestFailedException exception)
        {
            return HealthCheckResult.Unhealthy("Azure Storage Queue service metadata is unavailable.", exception);
        }
        catch (OperationCanceledException exception)
        {
            return HealthCheckResult.Unhealthy("Azure Storage Queue service metadata read timed out.", exception);
        }
    }
}

public sealed class AzureBlobServiceMetadataHealthCheck(BlobServiceClient blobServiceClient) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await blobServiceClient.GetPropertiesAsync(cancellationToken);
            return HealthCheckResult.Healthy("Azure Blob Storage service metadata is readable.");
        }
        catch (RequestFailedException exception)
        {
            return HealthCheckResult.Unhealthy("Azure Blob Storage service metadata is unavailable.", exception);
        }
        catch (OperationCanceledException exception)
        {
            return HealthCheckResult.Unhealthy("Azure Blob Storage service metadata read timed out.", exception);
        }
    }
}
