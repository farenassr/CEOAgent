using System.Globalization;
using Aspire.Hosting.ApplicationModel;
using Microsoft.Extensions.Configuration;

namespace CeoAgent.AppHost.Configuration;

internal static class CeoAgentApplicationExtensions
{
    private const string LocalPostgresDefaultPassword = "postgres";
    private const string PostgresPasswordParameterName = "postgres-password";

    public static void AddCeoAgentApplication(this IDistributedApplicationBuilder builder, AppHostOptions options)
    {
        var openAIApiKey = builder.Configuration.GetConnectionString("openai");
        var deploymentEnvironmentName = builder.ResolveDeploymentEnvironmentName();
        var postgresPassword = builder.AddParameter(
            PostgresPasswordParameterName,
            LocalPostgresDefaultPassword,
            secret: true);
        var keyVault = builder.ExecutionContext.IsPublishMode
            ? builder.AddAzureKeyVault("keyvault")
            : null;

        var postgresDatabase = builder.AddConfiguredPostgres(options, postgresPassword);
        var storage = builder.AddAzureStorage(options.Resources.Storage!)
            .RunAsEmulator(emulator => emulator
                .WithBlobPort(options.Azurite.BlobPort)
                .WithQueuePort(options.Azurite.QueuePort)
                .WithTablePort(options.Azurite.TablePort));

        var queues = storage.AddQueues(options.Resources.Queues!);
        var blobs = storage.AddBlobs(options.Resources.Blobs!);

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
            AddPostgresConnectionEnvironment(apiService, publishKeyVault, options.Postgres);
            AddPostgresConnectionEnvironment(worker, publishKeyVault, options.Postgres);
            apiService.WaitFor(postgresDatabase);
            worker.WaitFor(postgresDatabase);
        }
        else
        {
            apiService.WithReference(postgresDatabase);
            worker.WithReference(postgresDatabase);
        }

        if (!string.IsNullOrWhiteSpace(openAIApiKey))
        {
            worker
                .WithEnvironment("OpenAI__ApiKey", openAIApiKey)
                .WithEnvironment("LlmProviders__OpenAI__ApiKeyReference", "config://OpenAI:ApiKey");
        }

        builder.AddProviderEnvironment(apiService, worker, keyVault, deploymentEnvironmentName);
        builder.AddKeycloakEnvironment(apiService, keyVault);
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

        if (options.PgAdmin.Enabled)
        {
            AddPgAdmin(builder, postgresServer, options, postgresPassword);
        }

        return postgresServer.AddDatabase(options.Postgres.DatabaseName!);
    }

    private static void AddPgAdmin(
        IDistributedApplicationBuilder builder,
        IResourceBuilder<PostgresServerResource> postgresServer,
        AppHostOptions options,
        IResourceBuilder<ParameterResource> postgresPassword)
    {
        if (builder.ExecutionContext.IsPublishMode)
        {
            builder.AddContainer("pgadmin", options.PgAdmin.Image!)
                .WithEnvironment("PGADMIN_DEFAULT_EMAIL", options.PgAdmin.DefaultEmail!)
                .WithEnvironment("PGADMIN_DEFAULT_PASSWORD", postgresPassword)
                .WithEnvironment("PGADMIN_LISTEN_PORT", options.PgAdmin.HostPort.ToString(CultureInfo.InvariantCulture))
                .WithHttpEndpoint(targetPort: options.PgAdmin.HostPort, name: "http")
                .WithEndpoint("http", endpoint => endpoint.IsExternal = true)
                .WaitFor(postgresServer);
            return;
        }

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
