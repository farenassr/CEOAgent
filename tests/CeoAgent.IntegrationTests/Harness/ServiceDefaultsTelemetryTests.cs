using CeoAgent.ServiceDefaults.Configuration;
using Shouldly;

namespace CeoAgent.IntegrationTests.Harness;

public sealed class ServiceDefaultsTelemetryTests
{
    [Test]
    public void ServiceDefaults_ExportsOpenTelemetrySignalsToAspireDashboard()
    {
        var repoRoot = FindRepositoryRoot();
        var serviceDefaultsExtensions = File.ReadAllText(Path.Combine(
            repoRoot,
            "src",
            "CeoAgent.ServiceDefaults",
            "Extensions.cs"));

        serviceDefaultsExtensions.ShouldContain("OTEL_EXPORTER_OTLP_ENDPOINT");
        serviceDefaultsExtensions.ShouldContain("logging.AddOtlpExporter();");
        serviceDefaultsExtensions.ShouldContain("metrics.AddOtlpExporter();");
        serviceDefaultsExtensions.ShouldContain("tracing.AddOtlpExporter();");
        serviceDefaultsExtensions.ShouldNotContain("serviceDefaultsOptions.Otlp.IsConfigured");
    }

    [Test]
    public void LangfuseOtlpEndpoint_UsesBaseOtelEndpoint()
    {
        var options = new LangfuseOptions
        {
            Host = "https://us.cloud.langfuse.com/",
            PublicKey = "pk-lf-test",
            SecretKey = "sk-lf-test",
        };

        options.GetOtlpEndpoint().ToString().ShouldBe("https://us.cloud.langfuse.com/api/public/otel");
    }

    [Test]
    public void LangfuseOtlpEndpoint_NormalizesExplicitTracesEndpoint()
    {
        var options = new LangfuseOptions
        {
            OtlpTracesEndpoint = "https://us.cloud.langfuse.com/api/public/otel/v1/traces",
            PublicKey = "pk-lf-test",
            SecretKey = "sk-lf-test",
        };

        options.GetOtlpEndpoint().ToString().ShouldBe("https://us.cloud.langfuse.com/api/public/otel");
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "CEOAgent.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new DirectoryNotFoundException("Could not find repository root.");
    }
}
