namespace CEOAgent.AppHost.Configuration;

internal static class LangfuseEnvironmentExtensions
{
    public static void AddLangfuseEnvironment(
        this IDistributedApplicationBuilder builder,
        IResourceBuilder<ProjectResource> apiService,
        IResourceBuilder<ProjectResource> worker)
    {
        if (builder.ExecutionContext.IsPublishMode)
        {
            var keyVault = builder.AddAzureKeyVault("keyvault").PublishAsExisting("kv-ceo-agent-dev", "rg-ceo-agent-dev");
            apiService
                .WithEnvironment("LANGFUSE_PUBLIC_KEY", keyVault.GetSecret("LangfusePublicKey"))
                .WithEnvironment("LANGFUSE_SECRET_KEY", keyVault.GetSecret("LangfuseSecretKey"));
            worker
                .WithEnvironment("LANGFUSE_PUBLIC_KEY", keyVault.GetSecret("LangfusePublicKey"))
                .WithEnvironment("LANGFUSE_SECRET_KEY", keyVault.GetSecret("LangfuseSecretKey"));
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
            .WithEnvironment("LANGFUSE_PUBLIC_KEY", publicKey)
            .WithEnvironment("LANGFUSE_SECRET_KEY", secretKey);
    }
}
