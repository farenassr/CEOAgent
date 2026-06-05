using CeoAgent.AppHost.Configuration;
using Microsoft.Extensions.Configuration;

var builder = DistributedApplication.CreateBuilder(args);

const int PgAdminHostPort = 5050;
const int PostgresHostPort = 55432;
const int AzuriteBlobPort = 10000;
const int AzuriteQueuePort = 10001;
const int AzuriteTablePort = 10002;

const string StorageResourceName = "storage";
const string QueuesResourceName = "queues";
const string BlobsResourceName = "blobs";

var postgresPassword = builder.AddParameter("postgres-password", "postgres", secret: true);
var whatsAppAppSecret = builder.AddParameter("whatsapp-app-secret", secret: true);
var whatsAppAccessToken = builder.AddParameter("whatsapp-access-token", secret: true);
var openAIApiKey = builder.Configuration.GetConnectionString("openai");

var adminApiKey = builder.AddParameter("admin-api-key", secret: true);
var adminCompanyId = builder.AddParameter("admin-company-id");

var postgres = builder.AddPostgres("postgres", password: postgresPassword, port: 5432)
    .WithHostPort(PostgresHostPort)
    .WithDataVolume("ceoagent-postgres-database-volume")
    .WithPgAdmin(pgAdmin => pgAdmin.WithHostPort(PgAdminHostPort))
    .AddDatabase("CeoAgent");

var storage = builder.AddAzureStorage(StorageResourceName)
    .RunAsEmulator(emulator => emulator
        .WithBlobPort(AzuriteBlobPort)
        .WithQueuePort(AzuriteQueuePort)
        .WithTablePort(AzuriteTablePort));

var queues = storage.AddQueues(QueuesResourceName);
var blobs = storage.AddBlobs(BlobsResourceName);

var langfuseHost = builder.AddParameter("langfuse-host");

var apiService = builder.AddProject<Projects.CeoAgent_ApiService>("api")
    .WithReference(postgres)
    .WithReference(queues)
    .WithReference(blobs)
    .WithEnvironment("WhatsApp__AppSecret", whatsAppAppSecret)
    .WithEnvironment("WhatsApp__AccessToken", whatsAppAccessToken)
    .WithEnvironment("ServiceDefaults__Langfuse__Host", langfuseHost)
    .WithEnvironment("AdminApiKey__Key", adminApiKey)
    .WithEnvironment("AdminApiKey__CompanyId", adminCompanyId)
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

if (!string.IsNullOrWhiteSpace(openAIApiKey))
{
    worker
        .WithEnvironment("OpenAI__ApiKey", openAIApiKey)
        .WithEnvironment("LlmProviders__OpenAI__ApiKeyReference", "config://OpenAI:ApiKey");
}

builder.AddLangfuseEnvironment(apiService, worker);

await builder.Build().RunAsync();
