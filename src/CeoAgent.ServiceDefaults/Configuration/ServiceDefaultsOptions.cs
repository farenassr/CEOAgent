namespace CeoAgent.ServiceDefaults.Configuration;

public sealed class ServiceDefaultsOptions
{
    public const string SectionName = "ServiceDefaults";

    public OtlpExporterOptions Otlp { get; set; } = new();

    public LangfuseOptions Langfuse { get; set; } = new();

    public static bool IsValid(ServiceDefaultsOptions options)
    {
        return IsValidOptionalUri(options.Otlp.Endpoint)
            && IsValidLangfuseOptions(options.Langfuse);
    }

    private static bool IsValidLangfuseOptions(LangfuseOptions options)
    {
        var hasAnyValue = !string.IsNullOrWhiteSpace(options.Host)
            || !string.IsNullOrWhiteSpace(options.OtlpTracesEndpoint)
            || !string.IsNullOrWhiteSpace(options.PublicKey)
            || !string.IsNullOrWhiteSpace(options.SecretKey);

        if (!hasAnyValue)
        {
            return true;
        }

        var hasEndpointSource = !string.IsNullOrWhiteSpace(options.Host)
            || !string.IsNullOrWhiteSpace(options.OtlpTracesEndpoint);

        return hasEndpointSource
            && !string.IsNullOrWhiteSpace(options.PublicKey)
            && !string.IsNullOrWhiteSpace(options.SecretKey)
            && IsValidOptionalUri(options.Host)
            && IsValidOptionalUri(options.OtlpTracesEndpoint);
    }

    private static bool IsValidOptionalUri(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            || Uri.TryCreate(value, UriKind.Absolute, out _);
    }
}

public sealed class OtlpExporterOptions
{
    public string? Endpoint { get; set; }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(Endpoint);
}

public sealed class LangfuseOptions
{
    public string? Host { get; set; }

    public string? OtlpTracesEndpoint { get; set; }

    public string? PublicKey { get; set; }

    public string? SecretKey { get; set; }

    public bool IsConfigured => (!string.IsNullOrWhiteSpace(Host) || !string.IsNullOrWhiteSpace(OtlpTracesEndpoint))
        && !string.IsNullOrWhiteSpace(PublicKey)
        && !string.IsNullOrWhiteSpace(SecretKey);

    public Uri GetOtlpTracesEndpoint()
    {
        if (!string.IsNullOrWhiteSpace(OtlpTracesEndpoint))
        {
            return new Uri(OtlpTracesEndpoint);
        }

        var host = Host!.TrimEnd('/');
        return new Uri($"{host}/api/public/otel/v1/traces");
    }
}
