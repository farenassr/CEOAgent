using CeoAgent.Adapters;
using CeoAgent.ApiService.Dependencies;
using CeoAgent.ApiService.Infrastructure.Company;
using CeoAgent.ApiService.Infrastructure.Correlation;
using CeoAgent.ApiService.Infrastructure.ErrorHandling;
using CeoAgent.ApiService.Infrastructure.Queues;
using CeoAgent.ApiService.Infrastructure.Queues.Abstractions;
using CeoAgent.ApiService.Infrastructure.Queues.Implementation;
using CeoAgent.ApiService.Modules.WhatsApp;
using CeoAgent.Application.Errors;
using CeoAgent.Infrastructure.DependencyInjection;
using CeoAgent.ServiceDefaults;
using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Scalar.AspNetCore;
using ZLogger;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();

// Add service defaults & Aspire client integrations.
builder.AddServiceDefaults();

builder.Logging.AddZLoggerConsole(options =>
{
    options.IncludeScopes = true;
    options.UseJsonFormatter();
});

builder.Logging.AddOpenTelemetry(logging =>
{
    logging.IncludeFormattedMessage = true;
    logging.IncludeScopes = true;
    logging.ParseStateValues = true;
});

if (!builder.Environment.IsEnvironment("Testing"))
{
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
        builder.Services.AddSingleton<IIncomingMessageJobEnqueuer, AzureIncomingMessageJobEnqueuer>();
        builder.Services.AddSingleton<IQueueDiagnosticsService, AzureQueueDiagnosticsService>();
        builder.Services.AddHostedService<AzureQueueProvisioner>();
    }

    if (builder.Configuration.GetConnectionString("blobs") is { Length: > 0 })
    {
        builder.AddAzureBlobServiceClient("blobs");
        builder.Services.AddAzureBlobServiceMetadataHealthCheck();
    }
}

// Add services to the container.
builder.Services.AddOptions<QueueDiagnosticsOptions>()
    .BindConfiguration(QueueDiagnosticsOptions.SectionName)
    .Validate(options => options.DefaultMaxMessages > 0 && options.DefaultMaxQueues > 0, "Queue diagnostics limits must be positive.")
    .ValidateOnStart();
builder.Services.TryAddSingleton<IIncomingMessageJobEnqueuer, UnavailableIncomingMessageJobEnqueuer>();
builder.Services.TryAddSingleton<IQueueDiagnosticsService, UnavailableQueueDiagnosticsService>();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton<CorrelationIdAccessor>();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddAdapters(builder.Configuration);
builder.Services.AddApi();
builder.Services.AddOptions<WhatsAppOptions>()
    .BindConfiguration(WhatsAppOptions.SectionName)
    .Validate(options => options.MaxWebhookBodyBytes > 0, "WhatsApp webhook body limit must be positive.")
    .ValidateOnStart();
builder.Services.AddScoped<WhatsAppWebhookIngestionService>();
builder.Services.AddSingleton<IWhatsAppSignatureValidator, WhatsAppSignatureValidator>();
builder.Services.AddSingleton<WhatsAppWebhookVerificationService>();
builder.Services.AddOpenApi();

var app = builder.Build();

// Configure the HTTP request pipeline.
app.UseForwardedHeaders();

if (!app.Environment.IsDevelopment() && !app.Environment.IsEnvironment("Testing"))
{
    app.UseHsts();
}

app.UseMiddleware<CorrelationIdMiddleware>();
app.UseExceptionHandler();
app.UseMiddleware<CompanyContextMiddleware>();
app.UseConfiguredCors();
app.UseRateLimiter();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference("/scalar");
}

if (app.Environment.IsEnvironment("Testing"))
{
    app.MapGet("/__test/not-found", _ => throw new NotFoundException("conversation", "missing"));
    app.MapGet("/__test/business-rule", _ => throw new BusinessRuleException("conversation_closed", "Conversation is already closed."));
    app.MapGet("/__test/concurrency", _ => throw new DbUpdateConcurrencyException("Concurrency conflict."));
    app.MapGet("/__test/cancelled", _ => throw new OperationCanceledException("Request cancelled."));
    app.MapGet("/__test/integration", _ => throw new IntegrationException("google_calendar", "Calendar unavailable."));
    app.MapGet("/__test/unexpected", _ => throw new InvalidOperationException("Unexpected failure."));
}

app.MapDefaultEndpoints();
app.UseFastEndpoints(options => options.Endpoints.Configurator = endpoint => endpoint.AllowAnonymous());

await app.RunAsync();
