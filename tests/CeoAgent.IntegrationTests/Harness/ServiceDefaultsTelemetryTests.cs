using CeoAgent.ServiceDefaults.Configuration;
using CeoAgent.ServiceDefaults.Telemetry;
using OpenTelemetry.Trace;
using Shouldly;
using System.Diagnostics;

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

    [Test]
    public void LangSmithOtlpEndpoint_DefaultsToLangSmithOtelTracesEndpoint()
    {
        var options = new LangSmithOptions
        {
            ApiKey = "lsv2_pt_test",
            Project = "CeoAgent",
        };

        options.GetOtlpEndpoint().ToString().ShouldBe("https://api.smith.langchain.com/otel/v1/traces");
    }

    [Test]
    public void LangSmithOtlpEndpoint_NormalizesExplicitTracesEndpoint()
    {
        var options = new LangSmithOptions
        {
            OtlpTracesEndpoint = "https://api.smith.langchain.com/otel/v1/traces",
            ApiKey = "lsv2_pt_test",
            Project = "CeoAgent",
        };

        options.GetOtlpEndpoint().ToString().ShouldBe("https://api.smith.langchain.com/otel/v1/traces");
    }

    [Test]
    public void LangSmithOtlpEndpoint_AppendsTracesPathForBaseOtelEndpoint()
    {
        var options = new LangSmithOptions
        {
            OtlpTracesEndpoint = "https://api.smith.langchain.com/otel",
            ApiKey = "lsv2_pt_test",
            Project = "CeoAgent",
        };

        options.GetOtlpEndpoint().ToString().ShouldBe("https://api.smith.langchain.com/otel/v1/traces");
    }

    [Test]
    public void LangSmithOptions_RequiresOnlyApiKeySecretWhenUsingDefaultEndpoint()
    {
        var options = new ServiceDefaultsOptions
        {
            LangSmith = new LangSmithOptions
            {
                ApiKey = "lsv2_pt_test",
            },
        };

        ServiceDefaultsOptions.IsValid(options).ShouldBeTrue();
    }

    [Test]
    public void LangSmithOptions_AllowsNonSecretSettingsWithoutApiKey()
    {
        var options = new ServiceDefaultsOptions
        {
            LangSmith = new LangSmithOptions
            {
                OtlpTracesEndpoint = "https://api.smith.langchain.com/otel",
                Project = "ceoagent-local",
            },
        };

        ServiceDefaultsOptions.IsValid(options).ShouldBeTrue();
        options.LangSmith.IsConfigured.ShouldBeFalse();
    }

    [Test]
    public void LangSmithOptions_BuildsLangSmithOtlpHeaders()
    {
        var options = new LangSmithOptions
        {
            ApiKey = "lsv2_pt_test",
            Project = "CeoAgent",
        };

        options.GetHeaders().ShouldBe("x-api-key=lsv2_pt_test,Langsmith-Project=CeoAgent");
    }

    [Test]
    public void AppSettings_RouteLangSmithToCeoAgentProject()
    {
        var repoRoot = FindRepositoryRoot();
        foreach (var appsettingsPath in new[]
        {
            Path.Combine(repoRoot, "src", "CeoAgent.ApiService", "appsettings.json"),
            Path.Combine(repoRoot, "src", "CeoAgent.ApiService", "appsettings.Development.json"),
            Path.Combine(repoRoot, "src", "CeoAgent.Worker", "appsettings.json"),
            Path.Combine(repoRoot, "src", "CeoAgent.Worker", "appsettings.Development.json"),
        })
        {
            var appsettings = File.ReadAllText(appsettingsPath);
            appsettings.ShouldContain("\"OtlpTracesEndpoint\": \"https://api.smith.langchain.com/otel/v1/traces\"");
            appsettings.ShouldContain("\"Project\": \"CeoAgent\"");
        }
    }

    [Test]
    public void ServiceDefaults_ExportsTracesToLangSmithWhenConfigured()
    {
        var repoRoot = FindRepositoryRoot();
        var serviceDefaultsExtensions = File.ReadAllText(Path.Combine(
            repoRoot,
            "src",
            "CeoAgent.ServiceDefaults",
            "Extensions.cs"));

        serviceDefaultsExtensions.ShouldContain("AddLangSmithExporterIfConfigured(serviceDefaultsOptions.LangSmith, tracing);");
        serviceDefaultsExtensions.ShouldContain("options.Endpoint = langSmithOptions.GetOtlpEndpoint();");
        serviceDefaultsExtensions.ShouldContain("options.Headers = langSmithOptions.GetHeaders();");
    }

    [Test]
    public void AzureQueueNoiseSampler_SamplesReceiveMessagesOncePerThirtySeconds()
    {
        var timeProvider = new ManualTimeProvider(new DateTimeOffset(2026, 6, 16, 0, 0, 0, TimeSpan.Zero));
        var sampler = new AzureQueueNoiseSuppressingSampler(
            new AlwaysOnSampler(),
            TimeSpan.FromSeconds(30),
            timeProvider);

        sampler.ShouldSample(CreateSamplingParameters("QueueClient.ReceiveMessages")).Decision
            .ShouldBe(SamplingDecision.RecordAndSample);

        timeProvider.Advance(TimeSpan.FromSeconds(10));
        sampler.ShouldSample(CreateSamplingParameters("QueueClient.ReceiveMessages")).Decision
            .ShouldBe(SamplingDecision.Drop);

        timeProvider.Advance(TimeSpan.FromSeconds(20));
        sampler.ShouldSample(CreateSamplingParameters("QueueClient.ReceiveMessages")).Decision
            .ShouldBe(SamplingDecision.RecordAndSample);
    }

    [Test]
    public void AzureQueueNoiseSampler_SamplesQueueMetadataOncePerThirtySeconds()
    {
        var timeProvider = new ManualTimeProvider(new DateTimeOffset(2026, 6, 16, 0, 0, 0, TimeSpan.Zero));
        var sampler = new AzureQueueNoiseSuppressingSampler(
            new AlwaysOnSampler(),
            TimeSpan.FromSeconds(30),
            timeProvider);

        sampler.ShouldSample(CreateSamplingParameters("QueueServiceClient.GetProperties")).Decision
            .ShouldBe(SamplingDecision.RecordAndSample);

        timeProvider.Advance(TimeSpan.FromSeconds(10));
        sampler.ShouldSample(CreateSamplingParameters("QueueServiceClient.GetProperties")).Decision
            .ShouldBe(SamplingDecision.Drop);
    }

    [Test]
    public void AzureQueueNoiseSampler_DoesNotThrottleAgentSpans()
    {
        var timeProvider = new ManualTimeProvider(new DateTimeOffset(2026, 6, 16, 0, 0, 0, TimeSpan.Zero));
        var sampler = new AzureQueueNoiseSuppressingSampler(
            new AlwaysOnSampler(),
            TimeSpan.FromSeconds(30),
            timeProvider);

        sampler.ShouldSample(CreateSamplingParameters("agent.iteration")).Decision
            .ShouldBe(SamplingDecision.RecordAndSample);
        sampler.ShouldSample(CreateSamplingParameters("llm.generation")).Decision
            .ShouldBe(SamplingDecision.RecordAndSample);
        sampler.ShouldSample(CreateSamplingParameters("tool.execution")).Decision
            .ShouldBe(SamplingDecision.RecordAndSample);
    }

    [Test]
    public void ServiceDefaults_ThrottlesAzureQueuePollingSpansWithoutChangingPolling()
    {
        var repoRoot = FindRepositoryRoot();
        var serviceDefaultsExtensions = File.ReadAllText(Path.Combine(
            repoRoot,
            "src",
            "CeoAgent.ServiceDefaults",
            "Extensions.cs"));

        serviceDefaultsExtensions.ShouldContain("new AzureQueueNoiseSuppressingSampler");
        serviceDefaultsExtensions.ShouldContain("TimeSpan.FromSeconds(30)");
    }

    [Test]
    public void WorkerProcessor_DelegatesLangSmithAndLangfuseInstrumentationToTelemetryHelper()
    {
        var repoRoot = FindRepositoryRoot();
        var processorSource = File.ReadAllText(Path.Combine(
            repoRoot,
            "src",
            "CeoAgent.Worker",
            "Jobs",
            "ProcessIncomingMessageJobProcessor.cs"));
        var telemetrySource = File.ReadAllText(Path.Combine(
            repoRoot,
            "src",
            "CeoAgent.Worker",
            "Jobs",
            "Telemetry",
            "ProcessIncomingMessageJobTelemetry.cs"));

        processorSource.ShouldNotContain("CeoAgentTelemetry.LangSmith");
        processorSource.ShouldNotContain("CeoAgentTelemetry.Langfuse");
        processorSource.ShouldContain("ProcessIncomingMessageJobTelemetry.");
        telemetrySource.ShouldContain("CeoAgentTelemetry.LangSmith");
        telemetrySource.ShouldContain("CeoAgentTelemetry.Langfuse");
    }

    [Test]
    public void AppHost_ForwardsLangSmithApiKeyFromAspireParameter()
    {
        var repoRoot = FindRepositoryRoot();
        var appHostSource = File.ReadAllText(Path.Combine(
            repoRoot,
            "src",
            "CeoAgent.AppHost",
            "Configuration",
            "LangSmithEnvironmentExtensions.cs"));

        appHostSource.ShouldContain("builder.AddParameter(\"langsmith-api-key\", secret: true)");
        appHostSource.ShouldContain("ServiceDefaults__LangSmith__ApiKey");
        appHostSource.ShouldContain("LangSmithApiKey");
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

    private static SamplingParameters CreateSamplingParameters(string name)
    {
        return new SamplingParameters(
            default,
            ActivityTraceId.CreateRandom(),
            name,
            ActivityKind.Client,
            [],
            []);
    }

    private sealed class ManualTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        private DateTimeOffset utcNow = utcNow;

        public override DateTimeOffset GetUtcNow()
        {
            return utcNow;
        }

        public void Advance(TimeSpan timeSpan)
        {
            utcNow += timeSpan;
        }
    }
}
