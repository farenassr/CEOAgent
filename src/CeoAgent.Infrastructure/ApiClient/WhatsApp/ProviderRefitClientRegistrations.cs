using Microsoft.Extensions.DependencyInjection;
using Refit;

namespace CeoAgent.Infrastructure.ApiClient.WhatsApp;

/// <summary>
/// Provides Refit HTTP client registration helpers for external provider
/// adapters, including provider-specific retry policies.
/// </summary>
public static class ProviderRefitClientRegistrations
{
    /// <summary>
    /// Registers a WhatsApp Cloud API Refit client without automatic retries.
    /// Message send operations are not provider-idempotent, so retries must be
    /// explicit at the workflow boundary.
    /// </summary>
    public static IHttpClientBuilder AddWhatsAppCloudClient<TClient>(
        this IServiceCollection services)
        where TClient : class
    {
        return services.AddRefitClient<TClient>();
    }
}
