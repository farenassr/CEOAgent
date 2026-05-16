using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.ServiceDiscovery;
using OpenTelemetry;
using OpenTelemetry.Exporter;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using System.Text;

namespace Microsoft.Extensions.Hosting;

// Adds common Aspire services: service discovery, resilience, health checks, and OpenTelemetry.
// This project should be referenced by each service project in your solution.
// To learn more about using this project, see https://aka.ms/dotnet/aspire/service-defaults
public static class Extensions
{
    private const string HealthEndpointPath = "/health";
    private const string AlivenessEndpointPath = "/alive";

    public static TBuilder AddServiceDefaults<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
    {
        builder.ConfigureOpenTelemetry();

        builder.AddDefaultHealthChecks();

        builder.Services.AddServiceDiscovery();

        builder.Services.ConfigureHttpClientDefaults(http =>
        {
            // Turn on resilience by default
            http.AddStandardResilienceHandler();

            // Turn on service discovery by default
            http.AddServiceDiscovery();
        });

        // Uncomment the following to restrict the allowed schemes for service discovery.
        // builder.Services.Configure<ServiceDiscoveryOptions>(options =>
        // {
        //     options.AllowedSchemes = ["https"];
        // });

        return builder;
    }

    public static TBuilder ConfigureOpenTelemetry<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
    {
        builder.Logging.AddOpenTelemetry(logging =>
        {
            logging.IncludeFormattedMessage = true;
            logging.IncludeScopes = true;
        });

        builder.Services.AddOpenTelemetry()
            .WithMetrics(metrics =>
            {
                metrics.AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddRuntimeInstrumentation();
            })
            .WithTracing(tracing =>
            {
                tracing.AddSource(builder.Environment.ApplicationName)
                    .AddSource("Microsoft.SemanticKernel*")
                    .AddSource("OpenAI.*")
                    .AddSource("CeoAgent.*")
                    .AddAspNetCoreInstrumentation(tracing =>
                        // Exclude health check requests from tracing
                        tracing.Filter = context =>
                            !context.Request.Path.StartsWithSegments(HealthEndpointPath)
                            && !context.Request.Path.StartsWithSegments(AlivenessEndpointPath)
                    )
                    // Uncomment the following line to enable gRPC instrumentation (requires the OpenTelemetry.Instrumentation.GrpcNetClient package)
                    //.AddGrpcClientInstrumentation()
                    .AddHttpClientInstrumentation();
            });

        builder.AddOpenTelemetryExporters();
        builder.AddLangfuseExporter();

        return builder;
    }

    private static TBuilder AddLangfuseExporter<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
    {
        var langfuseHost = builder.Configuration["LANGFUSE_HOST"];
        var langfusePublicKey = builder.Configuration["LANGFUSE_PUBLIC_KEY"];
        var langfuseSecretKey = builder.Configuration["LANGFUSE_SECRET_KEY"];

        if (string.IsNullOrWhiteSpace(langfuseHost)
            || string.IsNullOrWhiteSpace(langfusePublicKey)
            || string.IsNullOrWhiteSpace(langfuseSecretKey))
        {
            return builder;
        }

        var langfuseAuth = "Basic " + Convert.ToBase64String(
            Encoding.UTF8.GetBytes($"{langfusePublicKey}:{langfuseSecretKey}"));

        builder.Services.AddOpenTelemetry()
            .WithTracing(tracing => tracing.AddOtlpExporter(options =>
            {
                options.Endpoint = new Uri($"{langfuseHost.TrimEnd('/')}/api/public/otel/v1/traces");
                options.Protocol = OtlpExportProtocol.HttpProtobuf;
                options.Headers = $"Authorization={langfuseAuth}";
            }));

        return builder;
    }

    private static TBuilder AddOpenTelemetryExporters<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
    {
        var useOtlpExporter = !string.IsNullOrWhiteSpace(builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]);

        if (useOtlpExporter)
        {
            builder.Services.AddOpenTelemetry()
                .WithMetrics(metrics => metrics.AddOtlpExporter())
                .WithTracing(tracing => tracing.AddOtlpExporter());
        }

        // Uncomment the following lines to enable the Azure Monitor exporter (requires the Azure.Monitor.OpenTelemetry.AspNetCore package)
        //if (!string.IsNullOrEmpty(builder.Configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"]))
        //{
        //    builder.Services.AddOpenTelemetry()
        //       .UseAzureMonitor();
        //}

        return builder;
    }

    public static TBuilder AddDefaultHealthChecks<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
    {
        builder.Services.AddHealthChecks()
            // Add a default liveness check to ensure app is responsive
            .AddCheck("self", () => HealthCheckResult.Healthy(), ["live"]);

        return builder;
    }

    public static WebApplication MapDefaultEndpoints(this WebApplication app)
    {
        // Adding health checks endpoints to applications in non-development environments has security implications.
        // See https://aka.ms/dotnet/aspire/healthchecks for details before enabling these endpoints in non-development environments.
        // All health checks must pass for app to be considered ready to accept traffic after starting.
        app.MapGet(HealthEndpointPath, async (
                HealthCheckService healthCheckService,
                CancellationToken cancellationToken) =>
            {
                var report = await healthCheckService.CheckHealthAsync(cancellationToken);
                var statusCode = report.Status == HealthStatus.Healthy
                    ? StatusCodes.Status200OK
                    : StatusCodes.Status503ServiceUnavailable;

                return Results.Text(report.Status.ToString(), statusCode: statusCode);
            })
            .WithName("Health")
            .WithSummary("Checks API health.")
            .WithDescription("Returns 200 when required health checks are healthy, or 503 when one or more required checks are unhealthy.")
            .Produces(StatusCodes.Status200OK, contentType: "text/plain")
            .Produces(StatusCodes.Status503ServiceUnavailable, contentType: "text/plain");

        if (app.Environment.IsDevelopment())
        {
            // Only health checks tagged with the "live" tag must pass for app to be considered alive
            app.MapHealthChecks(AlivenessEndpointPath, new HealthCheckOptions
            {
                Predicate = r => r.Tags.Contains("live")
            });
        }

        return app;
    }
}
