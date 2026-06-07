using CeoAgent.Application.Abstractions.Company;
using CeoAgent.Application.Abstractions.AI;
using CeoAgent.Application.Abstractions.AITools.GoogleCalendar;
using CeoAgent.Application.Abstractions.OpenAI;
using CeoAgent.Infrastructure.ApiClient.WhatsApp;
using CeoAgent.Infrastructure.Implementation.Company;
using CeoAgent.Infrastructure.Implementation.AITools.GoogleCalendar;
using CeoAgent.Infrastructure.Implementation.AITools.Execution;
using CeoAgent.Infrastructure.Implementation.AITools.GoogleCalendar.Integration;
using CeoAgent.Infrastructure.Implementation.Messaging.WhatsApp;
using CeoAgent.Infrastructure.Implementation.OpenAI;
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

        services.AddScoped<ICompanyContextAccessor, CompanyContextAccessor>();
        services.AddScoped<ICompanyContext>(provider => provider.GetRequiredService<ICompanyContextAccessor>());
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
        services.AddSingleton<ISecretValueProvider, SecretValueProvider>();
        services.AddSingleton<IOpenAIResponsesClientFactory, OpenAIResponsesClientFactory>();
        services.AddScoped<IAgentRuntime, OpenAIAgentRuntime>();
        services.AddScoped<IGoogleCalendarServiceFactory, GoogleCalendarServiceFactory>();
        services.AddScoped<IGoogleCalendarIntegration, GoogleCalendarIntegration>();
        services.AddScoped<IMessageChannelIntegration>(provider =>
        {
            var integration = provider.GetRequiredService<WhatsAppCloudIntegration>();
            return integration;
        });

        return services;
    }

    private static IServiceCollection AddInfrastructureTooling(this IServiceCollection services)
    {
        services.AddScoped<CompanyToolRegistry>();
        services.AddScoped<GoogleCalendarToolExecutor>();
        services.AddScoped<ToolExecutionGatewayHelper>();
        services.AddScoped<IToolExecutor, CheckGoogleCalendarAvailabilityExecutor>();
        services.AddScoped<IToolExecutor, CreateGoogleCalendarReservationExecutor>();
        services.AddScoped<IToolExecutor, FindGoogleCalendarReservationsExecutor>();
        services.AddScoped<IToolExecutor, UpdateGoogleCalendarReservationExecutor>();
        services.AddScoped<IToolExecutor, CancelGoogleCalendarReservationExecutor>();
        services.AddScoped<ToolExecutionGateway>();

        return services;
    }
}
