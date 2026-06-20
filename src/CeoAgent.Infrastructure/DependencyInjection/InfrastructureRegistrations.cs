using CeoAgent.Application.Abstractions.Organization;
using CeoAgent.Application.Abstractions.AI;
using CeoAgent.Application.Abstractions.AITools;
using CeoAgent.Application.Abstractions.AITools.GoogleCalendar;
using CeoAgent.Application.Abstractions.OpenAI;
using CeoAgent.Application.Abstractions.Payments;
using CeoAgent.Application.Abstractions.Storage;
using Azure.Storage.Blobs;
using CeoAgent.Infrastructure.ApiClient.WhatsApp;
using CeoAgent.Infrastructure.Implementation.Organization;
using CeoAgent.Infrastructure.Implementation.AI;
using CeoAgent.Infrastructure.Implementation.AITools.GoogleCalendar;
using CeoAgent.Infrastructure.Implementation.AITools.Execution;
using CeoAgent.Infrastructure.Implementation.AITools.Handoff;
using CeoAgent.Infrastructure.Implementation.AITools.Payments;
using CeoAgent.Infrastructure.Implementation.AITools.GoogleCalendar.Integration;
using CeoAgent.Infrastructure.Implementation.Messaging;
using CeoAgent.Infrastructure.Implementation.Messaging.WhatsApp;
using CeoAgent.Infrastructure.Implementation.OpenAI;
using CeoAgent.Infrastructure.Implementation.Messaging.Payments;
using CeoAgent.Infrastructure.Implementation.Messaging.Storage;
using CeoAgent.Application.Abstractions.Secrets;
using CeoAgent.Infrastructure.Implementation.Secrets;
using CeoAgent.Infrastructure.Persistence;
using CeoAgent.Application.Abstractions.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace CeoAgent.Infrastructure.DependencyInjection;

public static class InfrastructureRegistrations
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.TryAddSingleton(configuration);
        services.AddOptions<PersistenceOptions>()
            .BindConfiguration(PersistenceOptions.SectionName)
            .Validate(PersistenceOptions.IsValid,
                "Persistence:InMemoryDatabaseName is required when Persistence:UseInMemoryDatabase is true.")
            .ValidateOnStart();
        services.AddSingleton(provider => provider.GetRequiredService<IOptions<PersistenceOptions>>().Value);

        services.AddScoped<IOrganizationContextAccessor, OrganizationContextAccessor>();
        services.AddScoped<IOrganizationContextProvider>(provider => provider.GetRequiredService<IOrganizationContextAccessor>());
        services.AddScoped<IWhatsAppChannelCredentialResolver, WhatsAppChannelCredentialResolver>();
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<ConversationAgentProfileImmutabilityInterceptor>();
        services.AddInfrastructureImplementations(configuration);
        services.AddInfrastructureTooling();

        services.AddDbContext<CeoAgentDbContext>((provider, options) =>
        {
            var persistenceOptions = provider.GetRequiredService<IOptions<PersistenceOptions>>().Value;
            options.AddInterceptors(provider.GetRequiredService<ConversationAgentProfileImmutabilityInterceptor>());

            if (persistenceOptions.UseInMemoryDatabase)
            {
                options.UseInMemoryDatabase(persistenceOptions.InMemoryDatabaseName);
                return;
            }

            var configuration = provider.GetRequiredService<IConfiguration>();
            var connectionString = configuration.GetConnectionString("CeoAgent") ?? configuration.GetConnectionString("DefaultConnection");

            options.UseNpgsql(connectionString)
                .UseSnakeCaseNamingConvention();
        });

        return services;
    }

    private static IServiceCollection AddInfrastructureImplementations(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddWhatsAppCloudClient<IWhatsAppCloudClient>()
            .ConfigureHttpClient(client =>
            {
                client.BaseAddress = new Uri(
                    configuration["WhatsApp:GraphApiBaseUrl"]
                    ?? "https://graph.facebook.com/v25.0");
                client.Timeout = TimeSpan.FromSeconds(30);
            });

        services.AddScoped<WhatsAppCloudIntegration>();
        services.AddMemoryCache();
        services.AddOptions<OpenAIAgentRuntimeOptions>()
            .BindConfiguration(OpenAIAgentRuntimeOptions.SectionName);
        services.AddOptions<AgentRuntimeOptions>()
            .BindConfiguration(AgentRuntimeOptions.SectionName);
        services.AddSingleton<ISecretValueProvider, SecretValueProvider>();
        services.AddOpenAIImplementation();
        services.AddGoogleCalendarImplementation();
        services.AddScoped<IBlobStorageService>(provider =>
            provider.GetService<BlobServiceClient>() is { } blobServiceClient
                ? new AzureBlobStorageService(blobServiceClient)
                : new UnavailableBlobStorageService());
        services.AddScoped<IStoredFileReader, BlobStoredFileReader>();
        services.AddScoped<IPaymentQrImageProvider, BlobPaymentQrImageProvider>();
        services.AddScoped<IMessageChannelIntegration>(provider =>
        {
            var integration = provider.GetRequiredService<WhatsAppCloudIntegration>();
            return integration;
        });
        services.AddScoped<IOutboundMessageDispatcher, OutboundMessageDispatcher>();

        return services;
    }

    private static IServiceCollection AddInfrastructureTooling(this IServiceCollection services)
    {
        services.AddScoped<AgentTurnContextAccessor>();
        services.AddScoped<AgentFunctionCatalog>();
        services.AddScoped<AgentToolDispatcher>();
        services.AddScoped<AgentFunctionInvocationGuard>();
        services.AddScoped<GoogleCalendarToolExecutor>();
        services.AddScoped<HumanHandoffToolExecutor>();
        services.AddScoped<PaymentInstructionDataReader>();
        services.AddScoped<PaymentInstructionDispatchService>();
        services.AddScoped<ReservationPaymentInstructionSender>();
        services.AddScoped<IAgentToolCatalog, CompositeAgentToolCatalog>();
        services.AddScoped<IDynamicAgentToolProvider, NoOpDynamicAgentToolProvider>();
        services.AddAgentToolsFromInfrastructureAssembly();

        return services;
    }

    private static IServiceCollection AddAgentToolsFromInfrastructureAssembly(this IServiceCollection services)
    {
        var agentToolType = typeof(IAgentTool);
        var agentToolTypes = typeof(InfrastructureRegistrations).Assembly
            .DefinedTypes
            .Where(type => type is { IsAbstract: false, IsInterface: false }
                && agentToolType.IsAssignableFrom(type))
            .Select(type => type.AsType())
            .ToArray();

        foreach (var implementationType in agentToolTypes)
        {
            services.AddScoped(typeof(IAgentTool), implementationType);

            foreach (var serviceType in implementationType.GetInterfaces()
                         .Where(type => type.IsGenericType
                             && type.GetGenericTypeDefinition() == typeof(IAgentTool<>)))
            {
                services.AddScoped(serviceType, implementationType);
            }
        }

        return services;
    }
}
