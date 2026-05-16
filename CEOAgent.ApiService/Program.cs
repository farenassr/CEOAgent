using CEOAgent.ApiService;
using CEOAgent.Application.Errors;
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;
using ZLogger;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddZLoggerConsole();

// Add service defaults & Aspire client integrations.
builder.AddServiceDefaults();

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

// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var app = builder.Build();

// Configure the HTTP request pipeline.
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference("/api");
}

if (app.Environment.IsEnvironment("Testing"))
{
    app.MapGet("/__test/not-found", (HttpContext _) => throw new NotFoundException("reservation", "missing"));
    app.MapGet("/__test/business-rule", (HttpContext _) => throw new BusinessRuleException("reservation_closed", "Reservation is already closed."));
    app.MapGet("/__test/concurrency", (HttpContext _) => throw new DbUpdateConcurrencyException("Concurrency conflict."));
    app.MapGet("/__test/cancelled", (HttpContext _) => throw new OperationCanceledException("Request cancelled."));
    app.MapGet("/__test/integration", (HttpContext _) => throw new IntegrationException("google_calendar", "Calendar unavailable."));
    app.MapGet("/__test/unexpected", (HttpContext _) => throw new InvalidOperationException("Unexpected failure."));
}

app.MapDefaultEndpoints();

app.Run();

static bool HasAspireOpenAIConnectionString(string? connectionString)
{
    return !string.IsNullOrWhiteSpace(connectionString)
        && connectionString.Contains('=', StringComparison.Ordinal);
}

public partial class Program;
