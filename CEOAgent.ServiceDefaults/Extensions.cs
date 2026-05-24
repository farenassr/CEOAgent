using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using CEOAgent.ServiceDefaults.Configuration;
using OpenTelemetry.Exporter;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using System.Text;

namespace CEOAgent.ServiceDefaults;

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
        var serviceDefaultsOptions = ConfigureServiceDefaultsOptions(builder);

        builder.Logging.AddOpenTelemetry(logging =>
        {
            logging.IncludeFormattedMessage = true;
            logging.IncludeScopes = true;
        });

        var useOtlpExporter = serviceDefaultsOptions.Otlp.IsConfigured;

        builder.Services.AddOpenTelemetry()
            .WithMetrics(metrics =>
            {
                metrics.AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddRuntimeInstrumentation();

                if (useOtlpExporter)
                {
                    metrics.AddOtlpExporter();
                }
            })
            .WithTracing(tracing =>
            {
                tracing.AddSource(builder.Environment.ApplicationName)
                    .AddSource("Microsoft.AgentFramework*")
                    .AddSource("Microsoft.Extensions.AI*")
                    .AddSource("CeoAgent.*")
                    .AddAspNetCoreInstrumentation(tracing =>
                    {
                        tracing.Filter = context =>
                            !context.Request.Path.StartsWithSegments(HealthEndpointPath, StringComparison.Ordinal)
                            && !context.Request.Path.StartsWithSegments(AlivenessEndpointPath, StringComparison.Ordinal);
                    })
                    // Uncomment the following line to enable gRPC instrumentation (requires the OpenTelemetry.Instrumentation.GrpcNetClient package)
                    //.AddGrpcClientInstrumentation()
                    .AddHttpClientInstrumentation();

                if (useOtlpExporter)
                {
                    tracing.AddOtlpExporter();
                }

                AddLangfuseExporterIfConfigured(serviceDefaultsOptions.Langfuse, tracing);
            });

        return builder;
    }

    private static ServiceDefaultsOptions ConfigureServiceDefaultsOptions<TBuilder>(TBuilder builder)
        where TBuilder : IHostApplicationBuilder
    {
        var options = new ServiceDefaultsOptions();
        builder.Configuration.Bind(ServiceDefaultsOptions.SectionName, options);

        builder.Services.AddOptions<ServiceDefaultsOptions>()
            .BindConfiguration(ServiceDefaultsOptions.SectionName)
            .Validate(ServiceDefaultsOptions.IsValid,
                "ServiceDefaults telemetry options must be complete and valid absolute URIs.")
            .ValidateOnStart();

        return options;
    }

    private static void AddLangfuseExporterIfConfigured(
        LangfuseOptions langfuseOptions,
        TracerProviderBuilder tracing)
    {
        if (!langfuseOptions.IsConfigured)
        {
            return;
        }

        var authString = Convert.ToBase64String(
            Encoding.UTF8.GetBytes($"{langfuseOptions.PublicKey}:{langfuseOptions.SecretKey}"));

        tracing.AddOtlpExporter(options =>
        {
            options.Endpoint = langfuseOptions.GetOtlpTracesEndpoint();
            options.Protocol = OtlpExportProtocol.HttpProtobuf;
            options.Headers = $"Authorization=Basic {authString},x-langfuse-ingestion-version=4";
        });
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
        app.MapHealthChecks(HealthEndpointPath);

        if (app.Environment.IsDevelopment())
        {
            // Only health checks tagged with the "live" tag must pass for app to be considered alive
            app.MapHealthChecks(AlivenessEndpointPath, new HealthCheckOptions
            {
                Predicate = r => r.Tags.Contains("live"),
            });
        }

        return app;
    }
}
