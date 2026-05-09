using CEOAgent.ApiService;
using Microsoft.EntityFrameworkCore;
using ZLogger;

var builder = WebApplication.CreateBuilder(args);

// Add service defaults & Aspire client integrations.
builder.AddServiceDefaults();

// Add services to the container.
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton<CorrelationIdAccessor>();
builder.Logging.ClearProviders();
builder.Logging.AddZLoggerConsole();

// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var app = builder.Build();

// Configure the HTTP request pipeline.
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
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

public partial class Program;
