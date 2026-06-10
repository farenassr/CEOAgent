namespace CeoAgent.AppHost.Configuration;

internal static class LangfuseEnvironmentExtensions
{
    public static void AddLangfuseEnvironment(
        this IDistributedApplicationBuilder builder,
        IResourceBuilder<ProjectResource> apiService,
        IResourceBuilder<ProjectResource> worker,
        IResourceBuilder<Aspire.Hosting.Azure.AzureKeyVaultResource>? keyVault = null)
    {
        if (builder.ExecutionContext.IsPublishMode)
        {
            ArgumentNullException.ThrowIfNull(keyVault);
            apiService
                .WithEnvironment("ServiceDefaults__Langfuse__PublicKey", keyVault.GetSecret("LangfusePublicKey"))
                .WithEnvironment("ServiceDefaults__Langfuse__SecretKey", keyVault.GetSecret("LangfuseSecretKey"));
            worker
                .WithEnvironment("ServiceDefaults__Langfuse__PublicKey", keyVault.GetSecret("LangfusePublicKey"))
                .WithEnvironment("ServiceDefaults__Langfuse__SecretKey", keyVault.GetSecret("LangfuseSecretKey"));
            return;
        }

        var publicKey = builder.AddParameter("langfuse-public-key", secret: true);
        var secretKey = builder.AddParameter("langfuse-secret-key", secret: true);
        AddLangfuseSecrets(apiService, publicKey, secretKey);
        AddLangfuseSecrets(worker, publicKey, secretKey);
    }

    private static void AddLangfuseSecrets(
        IResourceBuilder<ProjectResource> project,
        IResourceBuilder<ParameterResource> publicKey,
        IResourceBuilder<ParameterResource> secretKey)
    {
        project
            .WithEnvironment("ServiceDefaults__Langfuse__PublicKey", publicKey)
            .WithEnvironment("ServiceDefaults__Langfuse__SecretKey", secretKey);
    }
}
