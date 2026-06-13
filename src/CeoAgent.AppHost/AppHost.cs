using CeoAgent.AppHost.Configuration;
using Microsoft.Extensions.Configuration;

var builder = DistributedApplication.CreateBuilder(args);

const int PgAdminHostPort = 5050;
const int PostgresHostPort = 55432;
const int ApiServiceHttpsHostPort = 7584;
const int ApiServiceHttpHostPort = 5481;
const int AzuriteBlobPort = 10000;
const int AzuriteQueuePort = 10001;
const int AzuriteTablePort = 10002;

const string StorageResourceName = "storage";
const string QueuesResourceName = "queues";
const string BlobsResourceName = "blobs";
const string KeyVaultName = "kv-ceo-agent-dev";
const string KeyVaultResourceGroup = "rg-ceo-agent-dev";

var postgresPassword = builder.AddParameter("postgres-password", "postgres", secret: true);
var whatsAppAppSecret = builder.AddParameter("whatsapp-app-secret", secret: true);
var whatsAppAccessToken = builder.AddParameter("whatsapp-access-token", secret: true);
var laTerrazaGoogleCalendar = builder.AddParameter("la-terraza-google-calendar", secret: true);
var openAIApiKey = builder.Configuration.GetConnectionString("openai");

var keyVault = builder.ExecutionContext.IsPublishMode
    ? builder.AddAzureKeyVault("keyvault").PublishAsExisting(KeyVaultName, KeyVaultResourceGroup)
    : null;

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
    .WithEnvironment("GoogleCalendar__ServiceAccountJson", laTerrazaGoogleCalendar)
    .WithEnvironment("ServiceDefaults__Langfuse__Host", langfuseHost)
    .WithEndpoint("https", endpoint =>
    {
        endpoint.Port = ApiServiceHttpsHostPort;
    })
    .WithEndpoint("http", endpoint =>
    {
        endpoint.Port = ApiServiceHttpHostPort;
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
    .WithReference(postgres)
    .WithReference(queues)
    .WithReference(blobs)
    .WithEnvironment("WhatsApp__AccessToken", whatsAppAccessToken)
    .WithEnvironment("GoogleCalendar__ServiceAccountJson", laTerrazaGoogleCalendar)
    .WithEnvironment("ServiceDefaults__Langfuse__Host", langfuseHost)
    .WaitFor(apiService);

if (!string.IsNullOrWhiteSpace(openAIApiKey))
{
    worker
        .WithEnvironment("OpenAI__ApiKey", openAIApiKey)
        .WithEnvironment("LlmProviders__OpenAI__ApiKeyReference", "config://OpenAI:ApiKey");
}

AddKeycloakEnvironment(builder, apiService, keyVault);
builder.AddLangfuseEnvironment(apiService, worker, keyVault);

await builder.Build().RunAsync();

static void AddKeycloakEnvironment(
    IDistributedApplicationBuilder builder,
    IResourceBuilder<ProjectResource> apiService,
    IResourceBuilder<Aspire.Hosting.Azure.AzureKeyVaultResource>? keyVault)
{
    if (builder.ExecutionContext.IsPublishMode)
    {
        ArgumentNullException.ThrowIfNull(keyVault);
        apiService
            .WithEnvironment("Keycloak__ClientId", builder.Configuration["Keycloak:ClientId"] ?? string.Empty)
            .WithEnvironment("Keycloak__ServiceClientId", builder.Configuration["Keycloak:ServiceClientId"] ?? string.Empty)
            .WithEnvironment("Keycloak__Issuer", builder.Configuration["Keycloak:Issuer"] ?? string.Empty)
            .WithEnvironment("Keycloak__RedirectUri", builder.Configuration["Keycloak:RedirectUri"] ?? string.Empty)
            .WithEnvironment("Keycloak__ServiceClientSecret", keyVault.GetSecret("KeycloakServiceClientSecret"));
        return;
    }

    var keycloakServiceClientSecret = builder.AddParameter("keycloak-service-client-secret", secret: true);

    apiService
        .WithEnvironment("Keycloak__ClientId", builder.Configuration["Keycloak:ClientId"] ?? string.Empty)
        .WithEnvironment("Keycloak__ServiceClientId", builder.Configuration["Keycloak:ServiceClientId"] ?? string.Empty)
        .WithEnvironment("Keycloak__Issuer", builder.Configuration["Keycloak:Issuer"] ?? string.Empty)
        .WithEnvironment("Keycloak__RedirectUri", builder.Configuration["Keycloak:RedirectUri"] ?? string.Empty)
        .WithEnvironment("Keycloak__ServiceClientSecret", keycloakServiceClientSecret);
}
