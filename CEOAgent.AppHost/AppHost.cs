using CeoAgent.AppHost.Configuration;

var builder = DistributedApplication.CreateBuilder(args);

const int PgAdminHostPort = 5050;
const int PostgresHostPort = 55432;

var postgresPassword = builder.AddParameter("postgres-password", "postgres", secret: true);
var whatsAppAppSecret = builder.AddParameter("whatsapp-app-secret", secret: true);
var whatsAppAccessToken = builder.AddParameter("whatsapp-access-token", secret: true);

var postgres = builder.AddPostgres("postgres", password: postgresPassword, port: 5432)
    .WithHostPort(PostgresHostPort)
    .WithDataVolume()
    .WithPgAdmin(pgAdmin => pgAdmin.WithHostPort(PgAdminHostPort))
    .AddDatabase("CeoAgent");

var storage = builder.AddAzureStorage("storage").RunAsEmulator();
var queues = storage.AddQueues("queues");
var blobs = storage.AddBlobs("blobs");

var langfuseHost = builder.AddParameter("langfuse-host");

var apiService = builder.AddProject<Projects.CeoAgent_ApiService>("api")
    .WithReference(postgres)
    .WithReference(queues)
    .WithReference(blobs)
    .WithEnvironment("WhatsApp__AppSecret", whatsAppAppSecret)
    .WithEnvironment("WhatsApp__AccessToken", whatsAppAccessToken)
    .WithEnvironment("ServiceDefaults__Langfuse__Host", langfuseHost)
    .WithUrlForEndpoint("https", url =>
    {
        url.DisplayText = "Scalar API Reference";
        url.Url = "/scalar";
    })
    .WithHttpHealthCheck("/health");

var worker = builder.AddProject<Projects.CeoAgent_Worker>("worker")
    .WithReference(postgres)
    .WithReference(queues)
    .WithReference(blobs)
    .WithEnvironment("WhatsApp__AccessToken", whatsAppAccessToken)
    .WithEnvironment("ServiceDefaults__Langfuse__Host", langfuseHost)
    .WaitFor(apiService);

builder.AddLangfuseEnvironment(apiService, worker);

await builder.Build().RunAsync();
