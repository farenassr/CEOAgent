namespace CeoAgent.AppHost.Configuration;

internal static class KeycloakEnvironmentExtensions
{
    public static void AddKeycloakEnvironment(
        this IDistributedApplicationBuilder builder,
        IResourceBuilder<ProjectResource> apiService,
        IResourceBuilder<Aspire.Hosting.Azure.AzureKeyVaultResource>? keyVault,
        KeycloakAppHostOptions options,
        ApiServiceOptions apiServiceOptions)
    {
        if (builder.ExecutionContext.IsPublishMode)
        {
            ArgumentNullException.ThrowIfNull(keyVault);
            apiService
                .WithEnvironment("Keycloak__ClientId", options.ClientId!)
                .WithEnvironment("Keycloak__ServiceClientId", options.ServiceClientId!)
                .WithEnvironment("Keycloak__Issuer", options.Issuer!)
                .WithEnvironment("Keycloak__RedirectUri", options.RedirectUri!)
                .WithEnvironment("Keycloak__ServiceClientSecret", keyVault.GetSecret("KeycloakServiceClientSecret"));
            return;
        }

        var keycloak = builder.AddKeycloak(options.ResourceName!, options.HostPort)
            .WithEndpointProxySupport(false)
            .WithDataVolume(options.DataVolumeName!)
            .WithRealmImport(options.RealmImportPath!);
        var keycloakServiceClientSecret = builder.AddParameter(
            "keycloak-service-client-secret",
            "local-dev-only-service-secret",
            secret: true);

        apiService
            .WithEnvironment("Keycloak__ClientId", options.ClientId!)
            .WithEnvironment("Keycloak__ServiceClientId", options.ServiceClientId!)
            .WithEnvironment("Keycloak__Issuer", BuildLocalIssuer(options))
            .WithEnvironment("Keycloak__RedirectUri", BuildLocalRedirectUri(apiServiceOptions))
            .WithEnvironment("Keycloak__ServiceClientSecret", keycloakServiceClientSecret)
            .WaitFor(keycloak);
    }

    private static string BuildLocalIssuer(KeycloakAppHostOptions options)
    {
        return $"https://localhost:{options.HostPort}/realms/{options.Realm}";
    }

    private static string BuildLocalRedirectUri(ApiServiceOptions apiServiceOptions)
    {
        return $"http://localhost:{apiServiceOptions.HttpHostPort}/scalar/";
    }
}
