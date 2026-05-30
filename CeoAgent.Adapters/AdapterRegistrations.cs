using CeoAgent.Adapters.WhatsApp;
using CeoAgent.Adapters.WhatsApp.Abstractions;
using CeoAgent.Adapters.WhatsApp.Client;
using CeoAgent.Integrations.Messaging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CeoAgent.Adapters;

/// <summary>
/// Registers external adapter implementations and their HTTP clients.
/// </summary>
public static class AdapterRegistrations
{
    /// <summary>
    /// Adds WhatsApp Cloud messaging adapters, secret resolution, and Graph API client configuration.
    /// </summary>
    public static IServiceCollection AddAdapters(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddWhatsAppCloudRefitClient<IWhatsAppCloudRefitClient>()
            .ConfigureHttpClient(client =>
            {
                client.BaseAddress = new Uri(
                    configuration["WhatsApp:GraphApiBaseUrl"]
                    ?? "https://graph.facebook.com/v25.0");
            });

        services.AddHttpClient<WhatsAppCloudIntegration>();
        services.AddMemoryCache();
        services.AddSingleton<ISecretValueProvider, SecretValueProvider>();
        services.AddScoped<IMessageChannelIntegration>(provider =>
        {
            var integration = provider.GetRequiredService<WhatsAppCloudIntegration>();
            return integration;
        });

        return services;
    }
}
