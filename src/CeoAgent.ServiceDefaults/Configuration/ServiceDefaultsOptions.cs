namespace CeoAgent.ServiceDefaults.Configuration;

public sealed class ServiceDefaultsOptions
{
    public const string SectionName = "ServiceDefaults";

    public OtlpExporterOptions Otlp { get; set; } = new();

    public LangfuseOptions Langfuse { get; set; } = new();

    public LangSmithOptions LangSmith { get; set; } = new();

    public static bool IsValid(ServiceDefaultsOptions options)
    {
        return IsValidOptionalUri(options.Otlp.Endpoint)
            && IsValidLangfuseOptions(options.Langfuse)
            && IsValidLangSmithOptions(options.LangSmith);
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

    private static bool IsValidLangSmithOptions(LangSmithOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.ApiKey))
        {
            return IsValidOptionalUri(options.OtlpTracesEndpoint);
        }

        return IsValidOptionalUri(options.OtlpTracesEndpoint);
    }

    private static bool IsValidOptionalUri(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            || Uri.TryCreate(value, UriKind.Absolute, out _);
    }
}

public sealed class LangSmithOptions
{
    private const string DefaultOtlpEndpoint = "https://api.smith.langchain.com/otel/v1/traces";
    private const string OtlpTracesPath = "/v1/traces";

    public string? OtlpTracesEndpoint { get; set; }

    public string? ApiKey { get; set; }

    public string? Project { get; set; }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(ApiKey);

    public Uri GetOtlpEndpoint()
    {
        var endpoint = string.IsNullOrWhiteSpace(OtlpTracesEndpoint)
            ? DefaultOtlpEndpoint
            : OtlpTracesEndpoint.TrimEnd('/');

        if (!endpoint.EndsWith(OtlpTracesPath, StringComparison.OrdinalIgnoreCase))
        {
            endpoint = $"{endpoint}{OtlpTracesPath}";
        }

        return new Uri(endpoint);
    }

    public string GetHeaders()
    {
        var headers = $"x-api-key={ApiKey}";
        if (!string.IsNullOrWhiteSpace(Project))
        {
            headers += $",Langsmith-Project={Project}";
        }

        return headers;
    }
}

public sealed class OtlpExporterOptions
{
    public string? Endpoint { get; set; }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(Endpoint);
}

public sealed class LangfuseOptions
{
    private const string OtlpTracesPath = "/v1/traces";

    public string? Host { get; set; }

    public string? OtlpTracesEndpoint { get; set; }

    public string? PublicKey { get; set; }

    public string? SecretKey { get; set; }

    public bool IsConfigured => (!string.IsNullOrWhiteSpace(Host) || !string.IsNullOrWhiteSpace(OtlpTracesEndpoint))
        && !string.IsNullOrWhiteSpace(PublicKey)
        && !string.IsNullOrWhiteSpace(SecretKey);

    public Uri GetOtlpEndpoint()
    {
        if (!string.IsNullOrWhiteSpace(OtlpTracesEndpoint))
        {
            var endpoint = OtlpTracesEndpoint.TrimEnd('/');
            if (endpoint.EndsWith(OtlpTracesPath, StringComparison.OrdinalIgnoreCase))
            {
                endpoint = endpoint[..^OtlpTracesPath.Length].TrimEnd('/');
            }

            return new Uri(endpoint);
        }

        var host = Host!.TrimEnd('/');
        return new Uri($"{host}/api/public/otel");
    }
}
