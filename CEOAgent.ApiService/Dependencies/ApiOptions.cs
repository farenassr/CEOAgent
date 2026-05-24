namespace CEOAgent.ApiService.Dependencies;

public sealed class ApiOptions
{
    public const string SectionName = "Api";

    public ApiCorsOptions Cors { get; set; } = new();

    public ApiRateLimitingOptions RateLimiting { get; set; } = new();

    public static bool IsValid(ApiOptions options)
    {
        return options.RateLimiting.PermitLimit > 0
            && options.RateLimiting.QueueLimit >= 0
            && options.RateLimiting.WindowSeconds > 0;
    }
}

public sealed class ApiCorsOptions
{
    public string[] AllowedOrigins { get; set; } = [];
}

public sealed class ApiRateLimitingOptions
{
    public bool AutoReplenishment { get; set; } = true;

    public int PermitLimit { get; set; } = 120;

    public int QueueLimit { get; set; }

    public int WindowSeconds { get; set; } = 60;
}
