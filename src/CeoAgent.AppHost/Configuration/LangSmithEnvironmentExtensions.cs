namespace CeoAgent.AppHost.Configuration;

internal static class LangSmithEnvironmentExtensions
{
    public static void AddLangSmithEnvironment(
        this IDistributedApplicationBuilder builder,
        IResourceBuilder<ProjectResource> apiService,
        IResourceBuilder<ProjectResource> worker,
        IResourceBuilder<Aspire.Hosting.Azure.AzureKeyVaultResource>? keyVault = null)
    {
        if (builder.ExecutionContext.IsPublishMode)
        {
            ArgumentNullException.ThrowIfNull(keyVault);
            AddLangSmithSecret(apiService, keyVault.GetSecret("LangSmithApiKey"));
            AddLangSmithSecret(worker, keyVault.GetSecret("LangSmithApiKey"));
            return;
        }

        var apiKey = builder.AddParameter("langsmith-api-key", secret: true);
        AddLangSmithSecret(apiService, apiKey);
        AddLangSmithSecret(worker, apiKey);
    }

    private static void AddLangSmithSecret(
        IResourceBuilder<ProjectResource> project,
        IResourceBuilder<ParameterResource> apiKey)
    {
        project.WithEnvironment("ServiceDefaults__LangSmith__ApiKey", apiKey);
    }

    private static void AddLangSmithSecret(
        IResourceBuilder<ProjectResource> project,
        Aspire.Hosting.Azure.IAzureKeyVaultSecretReference apiKey)
    {
        project.WithEnvironment("ServiceDefaults__LangSmith__ApiKey", apiKey);
    }
}
