namespace CeoAgent.AppHost.Configuration;

internal static class KeycloakEnvironmentExtensions
{
    public static void AddKeycloakEnvironment(
        this IDistributedApplicationBuilder builder,
        IResourceBuilder<ProjectResource> apiService,
        IResourceBuilder<Aspire.Hosting.Azure.AzureKeyVaultResource>? keyVault)
    {
        if (builder.ExecutionContext.IsPublishMode)
        {
            ArgumentNullException.ThrowIfNull(keyVault);
            apiService
                .WithEnvironment("Keycloak__ClientId", builder.Configuration["Keycloak:ClientId"] ?? string.Empty)
                .WithEnvironment("Keycloak__ServiceClientId", builder.Configuration["Keycloak:ServiceClientId"] ?? string.Empty)
                .WithEnvironment("Keycloak__Issuer", builder.Configuration["Keycloak:Issuer"] ?? string.Empty)
                .WithEnvironment("Keycloak__RedirectUri", builder.Configuration["Keycloak:RedirectUri"] ?? string.Empty)
                .WithEnvironment("Keycloak__ServiceClientSecret", keyVault.GetSecret("KeycloakServiceClientSecret"));
            return;
        }

        var keycloakServiceClientSecret = builder.AddParameter("keycloak-service-client-secret", secret: true);

        apiService
            .WithEnvironment("Keycloak__ClientId", builder.Configuration["Keycloak:ClientId"] ?? string.Empty)
            .WithEnvironment("Keycloak__ServiceClientId", builder.Configuration["Keycloak:ServiceClientId"] ?? string.Empty)
            .WithEnvironment("Keycloak__Issuer", builder.Configuration["Keycloak:Issuer"] ?? string.Empty)
            .WithEnvironment("Keycloak__RedirectUri", builder.Configuration["Keycloak:RedirectUri"] ?? string.Empty)
            .WithEnvironment("Keycloak__ServiceClientSecret", keycloakServiceClientSecret);
    }
}
