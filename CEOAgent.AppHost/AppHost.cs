var builder = DistributedApplication.CreateBuilder(args);

var postgres = builder.AddPostgres("postgres", port: 5432)
    .WithDataVolume()
    .WithPgAdmin()
    .AddDatabase("CEOAgent");

var storage = builder.AddAzureStorage("storage").RunAsEmulator();
var queues = storage.AddQueues("queues");
var blobs = storage.AddBlobs("blobs");

var openai = builder.AddConnectionString("openai");

var langfuseHost = builder.AddParameter("langfuse-host");

var localAdminApiKey = builder.AddParameter("admin-api-key", secret: true);
var localLangfusePublicKey = builder.AddParameter("langfuse-public-key", secret: true);
var localLangfuseSecretKey = builder.AddParameter("langfuse-secret-key", secret: true);

var apiService = builder.AddProject<Projects.CEOAgent_ApiService>("api")
    .WithReference(postgres)
    .WithReference(queues)
    .WithReference(blobs)
    .WithReference(openai)
    .WithEnvironment("LANGFUSE_HOST", langfuseHost)
    .WithUrlForEndpoint("https", url =>
    {
        url.DisplayText = "Scalar API Reference";
        url.Url = "/api";
    })
    .WithHttpHealthCheck("/health");

var worker = builder.AddProject<Projects.CEOAgent_Worker>("worker")
    .WithReference(postgres)
    .WithReference(queues)
    .WithReference(blobs)
    .WithReference(openai)
    .WithEnvironment("LANGFUSE_HOST", langfuseHost)
    .WaitFor(apiService);

if (builder.ExecutionContext.IsPublishMode)
{
    var keyVault = builder.AddAzureKeyVault("keyvault").PublishAsExisting("kv-ceo-agent-dev", "rg-ceo-agent-dev");

    apiService
        .WithEnvironment("Authentication__AdminApiKey", keyVault.GetSecret("AdminApiKey"))
        .WithEnvironment("LANGFUSE_PUBLIC_KEY", keyVault.GetSecret("LangfusePublicKey"))
        .WithEnvironment("LANGFUSE_SECRET_KEY", keyVault.GetSecret("LangfuseSecretKey"));

    worker
        .WithEnvironment("LANGFUSE_PUBLIC_KEY", keyVault.GetSecret("LangfusePublicKey"))
        .WithEnvironment("LANGFUSE_SECRET_KEY", keyVault.GetSecret("LangfuseSecretKey"));
}
else
{
    apiService
        .WithEnvironment("Authentication__AdminApiKey", localAdminApiKey)
        .WithEnvironment("LANGFUSE_PUBLIC_KEY", localLangfusePublicKey)
        .WithEnvironment("LANGFUSE_SECRET_KEY", localLangfuseSecretKey);

    worker
        .WithEnvironment("LANGFUSE_PUBLIC_KEY", localLangfusePublicKey)
        .WithEnvironment("LANGFUSE_SECRET_KEY", localLangfuseSecretKey);
}

builder.Build().Run();
