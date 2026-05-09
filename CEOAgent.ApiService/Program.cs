<<<<<<< HEAD
=======
using CEOAgent.ApiService;
using Microsoft.EntityFrameworkCore;
using ZLogger;

>>>>>>> 6e4100a (chore: add pgadmin to postgres apphost, fix: avoid mixed otlp exporter registration, chore: organize api runtime classes, chore: add runtime shell and observability. Add project files.)
var builder = WebApplication.CreateBuilder(args);

// Add service defaults & Aspire client integrations.
builder.AddServiceDefaults();

// Add services to the container.
builder.Services.AddProblemDetails();
<<<<<<< HEAD
=======
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton<CorrelationIdAccessor>();
builder.Logging.ClearProviders();
builder.Logging.AddZLoggerConsole();
>>>>>>> 6e4100a (chore: add pgadmin to postgres apphost, fix: avoid mixed otlp exporter registration, chore: organize api runtime classes, chore: add runtime shell and observability. Add project files.)

// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var app = builder.Build();

// Configure the HTTP request pipeline.
<<<<<<< HEAD
=======
app.UseMiddleware<CorrelationIdMiddleware>();
>>>>>>> 6e4100a (chore: add pgadmin to postgres apphost, fix: avoid mixed otlp exporter registration, chore: organize api runtime classes, chore: add runtime shell and observability. Add project files.)
app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

<<<<<<< HEAD
string[] summaries = ["Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"];

app.MapGet("/", () => "API service is running. Navigate to /weatherforecast to see sample data.");

app.MapGet("/weatherforecast", () =>
{
    var forecast = Enumerable.Range(1, 5).Select(index =>
        new WeatherForecast
        (
            DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
            Random.Shared.Next(-20, 55),
            summaries[Random.Shared.Next(summaries.Length)]
        ))
        .ToArray();
    return forecast;
})
.WithName("GetWeatherForecast");
=======
if (app.Environment.IsEnvironment("Testing"))
{
    app.MapGet("/__test/not-found", (HttpContext _) => throw new NotFoundException("reservation", "missing"));
    app.MapGet("/__test/business-rule", (HttpContext _) => throw new BusinessRuleException("reservation_closed", "Reservation is already closed."));
    app.MapGet("/__test/concurrency", (HttpContext _) => throw new DbUpdateConcurrencyException("Concurrency conflict."));
    app.MapGet("/__test/cancelled", (HttpContext _) => throw new OperationCanceledException("Request cancelled."));
    app.MapGet("/__test/integration", (HttpContext _) => throw new IntegrationException("google_calendar", "Calendar unavailable."));
    app.MapGet("/__test/unexpected", (HttpContext _) => throw new InvalidOperationException("Unexpected failure."));
}
>>>>>>> 6e4100a (chore: add pgadmin to postgres apphost, fix: avoid mixed otlp exporter registration, chore: organize api runtime classes, chore: add runtime shell and observability. Add project files.)

app.MapDefaultEndpoints();

app.Run();

<<<<<<< HEAD
record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}
=======
public partial class Program;
>>>>>>> 6e4100a (chore: add pgadmin to postgres apphost, fix: avoid mixed otlp exporter registration, chore: organize api runtime classes, chore: add runtime shell and observability. Add project files.)
