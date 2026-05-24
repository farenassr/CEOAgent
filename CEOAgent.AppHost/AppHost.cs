using CEOAgent.AppHost.Configuration;

var builder = DistributedApplication.CreateBuilder(args);

var postgres = builder.AddPostgres("postgres", port: 5432)
    .WithDataVolume()
    .WithPgAdmin()
    .AddDatabase("CEOAgent");

var storage = builder.AddAzureStorage("storage").RunAsEmulator();
var queues = storage.AddQueues("queues");
var blobs = storage.AddBlobs("blobs");

var langfuseHost = builder.AddParameter("langfuse-host");

var apiService = builder.AddProject<Projects.CEOAgent_ApiService>("api")
    .WithReference(postgres)
    .WithReference(queues)
    .WithReference(blobs)
    .WithEnvironment("ServiceDefaults__Langfuse__Host", langfuseHost)
    .WithUrlForEndpoint("https", url =>
    {
        url.DisplayText = "Scalar API Reference";
        url.Url = "/scalar";
    })
    .WithHttpHealthCheck("/health");

var worker = builder.AddProject<Projects.CEOAgent_Worker>("worker")
    .WithReference(postgres)
    .WithReference(queues)
    .WithReference(blobs)
    .WithEnvironment("ServiceDefaults__Langfuse__Host", langfuseHost)
    .WaitFor(apiService);

builder.AddLangfuseEnvironment(apiService, worker);

await builder.Build().RunAsync();
