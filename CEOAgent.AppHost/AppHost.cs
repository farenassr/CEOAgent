var builder = DistributedApplication.CreateBuilder(args);

var postgres = builder.AddPostgres("postgres")
    .WithDataVolume()
    .WithPgAdmin()
    .AddDatabase("CEOAgent");

var storage = builder.AddAzureStorage("storage").RunAsEmulator();
var queues = storage.AddQueues("queues");
var blobs = storage.AddBlobs("blobs");

var openai = builder.AddConnectionString("openai");

var langfuseHost = builder.AddParameter("langfuse-host");
var langfusePublicKey = builder.AddParameter("langfuse-public-key", secret: true);
var langfuseSecretKey = builder.AddParameter("langfuse-secret-key", secret: true);

var apiService = builder.AddProject<Projects.CEOAgent_ApiService>("api")
    .WithReference(postgres)
    .WithReference(queues)
    .WithReference(blobs)
    .WithReference(openai)
    .WithEnvironment("LANGFUSE_HOST", langfuseHost)
    .WithEnvironment("LANGFUSE_PUBLIC_KEY", langfusePublicKey)
    .WithEnvironment("LANGFUSE_SECRET_KEY", langfuseSecretKey)
    .WithUrlForEndpoint("https", url =>
    {
        url.DisplayText = "Scalar API Reference";
        url.Url = "/api";
    })
    .WithHttpHealthCheck("/health");

builder.AddProject<Projects.CEOAgent_Worker>("worker")
    .WithReference(postgres)
    .WithReference(queues)
    .WithReference(blobs)
    .WithReference(openai)
    .WithEnvironment("LANGFUSE_HOST", langfuseHost)
    .WithEnvironment("LANGFUSE_PUBLIC_KEY", langfusePublicKey)
    .WithEnvironment("LANGFUSE_SECRET_KEY", langfuseSecretKey)
    .WaitFor(apiService);

builder.Build().Run();
