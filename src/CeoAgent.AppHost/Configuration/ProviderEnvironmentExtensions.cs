namespace CeoAgent.AppHost.Configuration;

internal static class ProviderEnvironmentExtensions
{
    public static void AddProviderEnvironment(
        this IDistributedApplicationBuilder builder,
        IResourceBuilder<ProjectResource> apiService,
        IResourceBuilder<ProjectResource> worker,
        IResourceBuilder<Aspire.Hosting.Azure.AzureKeyVaultResource>? keyVault,
        string deploymentEnvironmentName)
    {
        if (builder.ExecutionContext.IsPublishMode)
        {
            ArgumentNullException.ThrowIfNull(keyVault);
            var langfuseHost = builder.Configuration["ServiceDefaults:Langfuse:Host"] ?? string.Empty;
            var applyApiMigrationsOnStartup = ShouldApplyApiMigrationsOnStartup(deploymentEnvironmentName) ? "true" : "false";

            apiService
                .WithEnvironment("ASPNETCORE_ENVIRONMENT", deploymentEnvironmentName)
                .WithEnvironment("DOTNET_ENVIRONMENT", deploymentEnvironmentName)
                .WithEnvironment("Persistence__ApplyMigrationsOnStartup", applyApiMigrationsOnStartup)
                .WithEnvironment("WhatsApp__AppSecret", keyVault.GetSecret("WhatsappAppSecret"))
                .WithEnvironment("WhatsApp__AccessToken", keyVault.GetSecret("WhatsappAccessToken"))
                .WithEnvironment("GoogleCalendar__ServiceAccountJson", keyVault.GetSecret("GoogleCalendarServiceAccount"))
                .WithEnvironment("ServiceDefaults__Langfuse__Host", langfuseHost);
            worker
                .WithEnvironment("DOTNET_ENVIRONMENT", deploymentEnvironmentName)
                .WithEnvironment("WhatsApp__AccessToken", keyVault.GetSecret("WhatsappAccessToken"))
                .WithEnvironment("GoogleCalendar__ServiceAccountJson", keyVault.GetSecret("GoogleCalendarServiceAccount"))
                .WithEnvironment("ServiceDefaults__Langfuse__Host", langfuseHost);
            return;
        }

        var whatsAppAppSecret = builder.AddParameter("whatsapp-app-secret", secret: true);
        var whatsAppAccessToken = builder.AddParameter("whatsapp-access-token", secret: true);
        var laTerrazaGoogleCalendar = builder.AddParameter("la-terraza-google-calendar", secret: true);
        var langfuseHostParameter = builder.AddParameter("langfuse-host");

        apiService
            .WithEnvironment("WhatsApp__AppSecret", whatsAppAppSecret)
            .WithEnvironment("WhatsApp__AccessToken", whatsAppAccessToken)
            .WithEnvironment("GoogleCalendar__ServiceAccountJson", laTerrazaGoogleCalendar)
            .WithEnvironment("ServiceDefaults__Langfuse__Host", langfuseHostParameter);
        worker
            .WithEnvironment("WhatsApp__AccessToken", whatsAppAccessToken)
            .WithEnvironment("GoogleCalendar__ServiceAccountJson", laTerrazaGoogleCalendar)
            .WithEnvironment("ServiceDefaults__Langfuse__Host", langfuseHostParameter);
    }

    public static string ResolveDeploymentEnvironmentName(this IDistributedApplicationBuilder builder)
    {
        if (!builder.ExecutionContext.IsPublishMode)
        {
            return builder.Environment.EnvironmentName;
        }

        var environmentName = builder.Configuration["AZURE_ENV_NAME"]
            ?? builder.Configuration["AZURE_ENVIRONMENT_NAME"]
            ?? builder.Configuration["Azure:EnvironmentName"];

        if (string.IsNullOrWhiteSpace(environmentName))
        {
            return "Production";
        }

        var tokens = environmentName
            .Split(['-', '_', '.'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (tokens.Any(token => string.Equals(token, "dev", StringComparison.OrdinalIgnoreCase)
            || string.Equals(token, "development", StringComparison.OrdinalIgnoreCase)))
        {
            return "Dev";
        }

        if (tokens.Any(token => string.Equals(token, "tst", StringComparison.OrdinalIgnoreCase)
            || string.Equals(token, "test", StringComparison.OrdinalIgnoreCase)))
        {
            return "Tst";
        }

        return "Production";
    }

    private static bool ShouldApplyApiMigrationsOnStartup(string deploymentEnvironmentName)
    {
        return string.Equals(deploymentEnvironmentName, "Dev", StringComparison.OrdinalIgnoreCase)
            || string.Equals(deploymentEnvironmentName, "Tst", StringComparison.OrdinalIgnoreCase)
            || string.Equals(deploymentEnvironmentName, "Development", StringComparison.OrdinalIgnoreCase)
            || string.Equals(deploymentEnvironmentName, "Local", StringComparison.OrdinalIgnoreCase);
    }
}
