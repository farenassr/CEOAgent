using CeoAgent.Infrastructure.DependencyInjection;
using CeoAgent.ServiceDefaults;
using CeoAgent.Worker;
using ZLogger;

var builder = Host.CreateApplicationBuilder(args);
builder.Logging.ClearProviders();
builder.Logging.AddZLoggerConsole(options =>
{
    options.IncludeScopes = true;
    options.UseJsonFormatter();
});
builder.AddServiceDefaults();
builder.AddCeoAgentPostgresConnectionString();

if (builder.Configuration.GetConnectionString("CeoAgent") is { Length: > 0 } postgresConnectionString)
{
    builder.Services
        .AddHealthChecks()
        .AddNpgSql(postgresConnectionString, name: "postgresql");
}

if (builder.Configuration.GetConnectionString("queues") is { Length: > 0 })
{
    builder.AddAzureQueueServiceClient("queues");
    builder.Services.AddAzureQueueServiceMetadataHealthCheck();
}

if (builder.Configuration.GetConnectionString("blobs") is { Length: > 0 })
{
    builder.AddAzureBlobServiceClient("blobs");
    builder.Services.AddAzureBlobServiceMetadataHealthCheck();
}

if (builder.Configuration.GetConnectionString("ollama-gemma-4-e2b-it-q4-k-m") is { Length: > 0 })
{
    var ollamaClientBuilder = builder.AddOllamaApiClient("ollama-gemma-4-e2b-it-q4-k-m");
    builder.Services
        .AddHttpClient("ollama-gemma-4-e2b-it-q4-k-m_httpClient")
        .RemoveAllResilienceHandlers()
        .ConfigureHttpClient(client => client.Timeout = Timeout.InfiniteTimeSpan);
    ollamaClientBuilder.AddChatClient();
}

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddWorkerRuntime();

var host = builder.Build();

await host.RunAsync();
