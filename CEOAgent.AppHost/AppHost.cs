var builder = DistributedApplication.CreateBuilder(args);

<<<<<<< HEAD
var apiService = builder.AddProject<Projects.CEOAgent_ApiService>("apiservice")
    .WithHttpHealthCheck("/health");

builder.AddProject<Projects.CEOAgent_Web>("webfrontend")
    .WithExternalHttpEndpoints()
    .WithHttpHealthCheck("/health")
    .WithReference(apiService)
=======
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
    .WithHttpHealthCheck("/health");

builder.AddProject<Projects.CEOAgent_Worker>("worker")
    .WithReference(postgres)
    .WithReference(queues)
    .WithReference(blobs)
    .WithReference(openai)
    .WithEnvironment("LANGFUSE_HOST", langfuseHost)
    .WithEnvironment("LANGFUSE_PUBLIC_KEY", langfusePublicKey)
    .WithEnvironment("LANGFUSE_SECRET_KEY", langfuseSecretKey)
>>>>>>> 6e4100a (chore: add pgadmin to postgres apphost, fix: avoid mixed otlp exporter registration, chore: organize api runtime classes, chore: add runtime shell and observability. Add project files.)
    .WaitFor(apiService);

builder.Build().Run();
