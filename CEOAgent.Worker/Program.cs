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

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddHostedService<Worker>();

var host = builder.Build();

await host.RunAsync();
