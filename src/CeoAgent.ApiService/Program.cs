using CeoAgent.ApiService.Dependencies;
using CeoAgent.ApiService.Infrastructure.Organization;
using CeoAgent.ApiService.Infrastructure.Correlation;
using CeoAgent.ApiService.Infrastructure.ErrorHandling;
using CeoAgent.ApiService.Infrastructure.OpenApi;
using CeoAgent.ApiService.Infrastructure.Queues;
using CeoAgent.ApiService.Infrastructure.Security;
using CeoAgent.ApiService.Infrastructure.Queues.Abstractions;
using CeoAgent.ApiService.Infrastructure.Queues.Implementation;
using CeoAgent.ApiService.Modules.WhatsApp;
using CeoAgent.Application.Errors;
using CeoAgent.Infrastructure.DependencyInjection;
using CeoAgent.ServiceDefaults;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Tokens;
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
        builder.Services.AddHostedService<IncomingMessageOutboxHostedService>();
    }

    if (builder.Configuration.GetConnectionString("blobs") is { Length: > 0 })
    {
        builder.AddAzureBlobServiceClient("blobs");
        builder.Services.AddAzureBlobServiceMetadataHealthCheck();
    }
}

// Add services to the container.
builder.Services.AddOptions<KeycloakOptions>()
    .BindConfiguration(KeycloakOptions.SectionName)
    .Validate(
        KeycloakOptions.IsValid,
        "Keycloak must configure ClientId and an absolute Issuer URI.")
    .ValidateOnStart();

builder.Services.AddOptions<QueueDiagnosticsOptions>()
    .BindConfiguration(QueueDiagnosticsOptions.SectionName)
    .Validate(options => options.DefaultMaxMessages > 0 && options.DefaultMaxQueues > 0, "Queue diagnostics limits must be positive.")
    .ValidateOnStart();
builder.Services.TryAddSingleton<IIncomingMessageJobEnqueuer, UnavailableIncomingMessageJobEnqueuer>();
builder.Services.TryAddSingleton<IQueueDiagnosticsService, UnavailableQueueDiagnosticsService>();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
var keycloakOptions = builder.Configuration
    .GetSection(KeycloakOptions.SectionName)
    .Get<KeycloakOptions>() ?? new KeycloakOptions();

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = keycloakOptions.Issuer;
        options.Audience = keycloakOptions.ClientId;
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = keycloakOptions.Issuer,
            ValidateAudience = true,
            ValidAudience = keycloakOptions.ClientId,
            NameClaimType = "preferred_username",
        };
    });
builder.Services.AddAuthorization();
builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton<AuthenticatedOrganizationContextProvider>();
builder.Services.AddSingleton<CorrelationIdAccessor>();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddApi();
builder.Services.AddOptions<WhatsAppOptions>()
    .BindConfiguration(WhatsAppOptions.SectionName)
    .Validate(options => options.MaxWebhookBodyBytes > 0, "WhatsApp webhook body limit must be positive.")
    .ValidateOnStart();
builder.Services.AddScoped<WhatsAppWebhookIngestionService>();
builder.Services.AddScoped<IncomingMessageOutboxDispatcher>();
builder.Services.AddSingleton<IWhatsAppSignatureValidator, WhatsAppSignatureValidator>();
builder.Services.AddSingleton<WhatsAppWebhookVerificationService>();
builder.Services.AddOpenApi(options =>
{
    options.AddDocumentTransformer<KeycloakOpenApiDocumentTransformer>();
    options.AddOperationTransformer<KeycloakOpenApiOperationTransformer>();
});

var app = builder.Build();

// Configure the HTTP request pipeline.
app.UseForwardedHeaders();

if (!app.Environment.IsDevelopment() && !app.Environment.IsEnvironment("Testing"))
{
    app.UseHsts();
}

// Explicit UseRouting must come before other middleware so that ShortCircuit()
// on health check endpoints bypasses exception handling, auth, rate limiting, etc.
app.UseRouting();
app.MapDefaultEndpoints();

app.UseMiddleware<CorrelationIdMiddleware>();
app.UseExceptionHandler();
app.UseRateLimiter();
app.UseConfiguredCors();
app.UseAuthentication();
app.UseMiddleware<OrganizationContextMiddleware>();
app.UseAuthorization();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference("/scalar", options =>
    {
        options
            .WithTitle("CeoAgent API Reference")
            .WithTheme(ScalarTheme.Default)
            .ForceLightMode()
            .WithDefaultHttpClient(ScalarTarget.CSharp, ScalarClient.HttpClient)
            .WithDefaultHttpClient(ScalarTarget.Shell, ScalarClient.Curl);
    });
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

app.UseFastEndpoints();

await app.RunAsync();
