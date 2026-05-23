using CEOAgent.ApiService;
using CEOAgent.ApiService.Infrastructure.Company;
using CEOAgent.ApiService.Infrastructure.Correlation;
using CEOAgent.ApiService.Infrastructure.ErrorHandling;
using CEOAgent.Application.Errors;
using CEOAgent.Infrastructure;
using CEOAgent.ServiceDefaults;
using FastEndpoints;
using Microsoft.EntityFrameworkCore;
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
}

// Add services to the container.
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton<CorrelationIdAccessor>();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddApiService(builder.Configuration);

// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var app = builder.Build();

// Configure the HTTP request pipeline.
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseExceptionHandler();
app.UseMiddleware<CompanyContextMiddleware>();
app.UseAuthentication();
app.UseAuthorization();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference("/api");
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
app.UseFastEndpoints();

app.Run();

static bool HasAspireOpenAIConnectionString(string? connectionString)
{
    return !string.IsNullOrWhiteSpace(connectionString)
        && connectionString.Contains('=', StringComparison.Ordinal);
}
