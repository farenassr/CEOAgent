using System.Globalization;

namespace CeoAgent.AppHost.Configuration;

internal static class CeoAgentApplicationExtensions
{
    private const string LocalPostgresDefaultPassword = "postgres";
    private const string PostgresPasswordParameterName = "postgres-password";
    private const string OpenAIApiKeyParameterName = "openai-api-key";
    private const string GeminiApiKeyParameterName = "gemini-api-key";

    public static void AddCeoAgentApplication(this IDistributedApplicationBuilder builder, AppHostOptions options)
    {
        var deploymentEnvironmentName = builder.ResolveDeploymentEnvironmentName();
        var postgresPassword = builder.AddParameter(
            PostgresPasswordParameterName,
            LocalPostgresDefaultPassword,
            secret: true);
        var openAIApiKey = builder.AddParameter(OpenAIApiKeyParameterName, secret: true);
        var geminiApiKey = builder.AddParameter(GeminiApiKeyParameterName, secret: true);
        var keyVault = builder.ExecutionContext.IsPublishMode
            ? builder.AddAzureKeyVault("keyvault")
            : null;

        var postgresDatabase = builder.AddConfiguredPostgres(options, postgresPassword);
        var storage = builder.AddAzureStorage(options.Resources.Storage!)
            .RunAsEmulator(emulator => emulator
                .WithBlobPort(options.Azurite.BlobPort)
                .WithQueuePort(options.Azurite.QueuePort)
                .WithTablePort(options.Azurite.TablePort)
                .WithDataVolume(options.Azurite.DataVolumeName!));

        var queues = storage.AddQueues(options.Resources.Queues!);
        var blobs = storage.AddBlobs(options.Resources.Blobs!);

        IResourceBuilder<OllamaModelResource>? ollamaModel = null;
        if (!builder.ExecutionContext.IsPublishMode)
        {
            //var ollama = builder.AddOllama(options.Ollama.ResourceName!)
            //    .WithImageTag(options.Ollama.ImageTag!)
            //    .WithDataVolume(options.Ollama.DataVolumeName!);
            //ollama.WithOpenWebUI(webui =>
            //{
            //    webui.WithDataVolume("ceoagent-openwebui-data")
            //        .WithUrlForEndpoint("http", url =>
            //        {
            //            url.DisplayText = "Open WebUI Chat";
            //            url.Url = "/";
            //        });
            //});
            //ollamaModel = ollama.AddModel(options.Ollama.ModelResourceName!, options.Ollama.ModelName!);
        }

        var apiService = builder.AddProject<Projects.CeoAgent_ApiService>("api")
            .WithReference(queues)
            .WithReference(blobs)
            .WithEndpoint("https", endpoint =>
            {
                if (!builder.ExecutionContext.IsPublishMode)
                {
                    endpoint.Port = options.ApiService.HttpsHostPort;
                }
            })
            .WithEndpoint("http", endpoint =>
            {
                if (!builder.ExecutionContext.IsPublishMode)
                {
                    endpoint.Port = options.ApiService.HttpHostPort;
                }
                else
                {
                    endpoint.IsExternal = true;
                }
            })
            .WithUrlForEndpoint("https", url =>
            {
                url.DisplayText = "Scalar API Reference";
                url.Url = "/scalar";
            })
            .WithUrlForEndpoint("http", url =>
            {
                url.DisplayText = "Scalar API Reference";
                url.Url = "/scalar";
            })
            .WithHttpHealthCheck("/health");

        var worker = builder.AddProject<Projects.CeoAgent_Worker>("worker")
            .WithReference(queues)
            .WithReference(blobs)
            .WaitFor(apiService);

        if (builder.ExecutionContext.IsPublishMode)
        {
            var publishKeyVault = keyVault ?? throw new InvalidOperationException("Key Vault is required for publish mode.");
            publishKeyVault.AddSecret(options.Postgres.PasswordSecretName!, postgresPassword);
            publishKeyVault.AddSecret("OpenAIApiKey", openAIApiKey);
            publishKeyVault.AddSecret("GeminiApiKey", geminiApiKey);
            AddPostgresConnectionEnvironment(apiService, publishKeyVault, options.Postgres);
            AddPostgresConnectionEnvironment(worker, publishKeyVault, options.Postgres);
            worker
                .WithEnvironment("Secrets__llm__openai__api-key", publishKeyVault.GetSecret("OpenAIApiKey"))
                .WithEnvironment("Secrets__llm__gemini__api-key", publishKeyVault.GetSecret("GeminiApiKey"))
                .WithEnvironment("LlmProviders__OpenAI__ApiKeyReference", "kv://llm/openai/api-key")
                .WithEnvironment("LlmProviders__Gemini__ApiKeyReference", "kv://llm/gemini/api-key");
            apiService.WaitFor(postgresDatabase);
            worker.WaitFor(postgresDatabase);
        }
        else
        {
            apiService
                .WithReference(postgresDatabase)
                .WaitFor(postgresDatabase);
            worker
                .WithReference(postgresDatabase)
                .WaitFor(postgresDatabase)
                .WithEnvironment("Secrets__llm__openai__api-key", openAIApiKey)
                .WithEnvironment("Secrets__llm__gemini__api-key", geminiApiKey)
                .WithEnvironment("LlmProviders__OpenAI__ApiKeyReference", "kv://llm/openai/api-key")
                .WithEnvironment("LlmProviders__Gemini__ApiKeyReference", "kv://llm/gemini/api-key");

            if (ollamaModel is not null)
            {
                worker.WithReference(ollamaModel)
                    .WaitFor(ollamaModel);
            }
        }

        builder.AddProviderEnvironment(apiService, worker, keyVault, deploymentEnvironmentName);
        builder.AddKeycloakEnvironment(apiService, keyVault, options.Keycloak, options.ApiService);
        builder.AddLangfuseEnvironment(apiService, worker, keyVault);
        builder.AddLangSmithEnvironment(apiService, worker, keyVault);
    }

    private static IResourceBuilder<PostgresDatabaseResource> AddConfiguredPostgres(
        this IDistributedApplicationBuilder builder,
        AppHostOptions options,
        IResourceBuilder<ParameterResource> postgresPassword)
    {
        var postgresServer = builder.ExecutionContext.IsPublishMode
            ? builder.AddPostgres(options.Postgres.ResourceName!, password: postgresPassword)
            : builder.AddPostgres(
                    options.Postgres.ResourceName!,
                    password: postgresPassword,
                    port: options.Postgres.Port)
                .WithHostPort(options.Postgres.HostPort)
                .WithDataVolume("ceoagent-postgres-database-volume");

        if (builder.ExecutionContext.IsPublishMode)
        {
            postgresServer.WithEnvironment("POSTGRES_DB", options.Postgres.DatabaseName!);
        }

        if (options.PgAdmin.Enabled && !builder.ExecutionContext.IsPublishMode)
        {
            AddPgAdmin(postgresServer, options);
        }

        return postgresServer.AddDatabase(options.Postgres.DatabaseName!);
    }

    private static void AddPgAdmin(
        IResourceBuilder<PostgresServerResource> postgresServer,
        AppHostOptions options)
    {
        postgresServer.WithPgAdmin(pgAdmin => pgAdmin.WithHostPort(options.PgAdmin.HostPort));
    }

    private static void AddPostgresConnectionEnvironment(
        IResourceBuilder<ProjectResource> project,
        IResourceBuilder<Aspire.Hosting.Azure.AzureKeyVaultResource> keyVault,
        PostgresOptions postgres)
    {
        project
            .WithEnvironment("Postgres__Host", postgres.Host!)
            .WithEnvironment("Postgres__Port", postgres.Port.ToString(CultureInfo.InvariantCulture))
            .WithEnvironment("Postgres__Username", postgres.Username!)
            .WithEnvironment("Postgres__Password", keyVault.GetSecret(postgres.PasswordSecretName!))
            .WithEnvironment("Postgres__Database", postgres.DatabaseName!);
    }
}
