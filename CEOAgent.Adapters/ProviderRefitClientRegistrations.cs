using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Polly;
using Refit;

namespace CEOAgent.Adapters;

/// <summary>
/// Provides Refit HTTP client registration helpers for external provider
/// adapters, including provider-specific retry policies.
/// </summary>
public static class ProviderRefitClientRegistrations
{
    /// <summary>
    /// Registers a WhatsApp Cloud API Refit client and configures an exponential
    /// retry policy that honors provider retry-after responses.
    /// </summary>
    public static IHttpClientBuilder AddWhatsAppCloudRefitClient<TClient>(
        this IServiceCollection services)
        where TClient : class
    {
        var builder = services.AddRefitClient<TClient>();

        builder.RemoveAllResilienceHandlers();
        builder.AddResilienceHandler("whatsapp-cloud", pipeline =>
        {
            pipeline.AddRetry(new HttpRetryStrategyOptions
            {
                BackoffType = DelayBackoffType.Exponential,
                Delay = TimeSpan.FromSeconds(1),
                MaxDelay = TimeSpan.FromSeconds(30),
                MaxRetryAttempts = 3,
                ShouldRetryAfterHeader = true,
                UseJitter = true,
            });
        });

        return builder;
    }

    /// <summary>
    /// Registers a Google Calendar API Refit client and configures a short
    /// exponential retry policy that honors provider retry-after responses.
    /// </summary>
    public static IHttpClientBuilder AddGoogleCalendarRefitClient<TClient>(
        this IServiceCollection services)
        where TClient : class
    {
        var builder = services.AddRefitClient<TClient>();

        builder.RemoveAllResilienceHandlers();
        builder.AddResilienceHandler("google-calendar", pipeline =>
        {
            pipeline.AddRetry(new HttpRetryStrategyOptions
            {
                BackoffType = DelayBackoffType.Exponential,
                Delay = TimeSpan.FromMilliseconds(500),
                MaxDelay = TimeSpan.FromSeconds(10),
                MaxRetryAttempts = 2,
                ShouldRetryAfterHeader = true,
                UseJitter = true,
            });
        });

        return builder;
    }
}
