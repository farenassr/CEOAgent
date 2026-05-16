using CEOAgent.Worker;
using ZLogger;

var builder = Host.CreateApplicationBuilder(args);
builder.Logging.ClearProviders();
builder.Logging.AddZLoggerConsole();
builder.AddServiceDefaults();

if (builder.Configuration.GetConnectionString("CEOAgent") is { Length: > 0 } postgresConnectionString)
{
    builder.Services
        .AddHealthChecks()
        .AddNpgSql(postgresConnectionString, name: "postgresql");
}

if (builder.Configuration.GetConnectionString("queues") is { Length: > 0 })
{
    builder.AddAzureQueueServiceClient("queues");
}

if (builder.Configuration.GetConnectionString("blobs") is { Length: > 0 })
{
    builder.AddAzureBlobServiceClient("blobs");
}

if (HasAspireOpenAIConnectionString(builder.Configuration.GetConnectionString("openai")))
{
    builder.AddOpenAIClient("openai");
}

builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();

static bool HasAspireOpenAIConnectionString(string? connectionString)
{
    return !string.IsNullOrWhiteSpace(connectionString)
        && connectionString.Contains('=', StringComparison.Ordinal);
}
