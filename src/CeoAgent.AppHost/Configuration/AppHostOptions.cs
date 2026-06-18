using Microsoft.Extensions.Configuration;

namespace CeoAgent.AppHost.Configuration;

internal sealed class AppHostOptions
{
    public ResourceNameOptions Resources { get; set; } = new();

    public PgAdminOptions PgAdmin { get; set; } = new();

    public PostgresOptions Postgres { get; set; } = new();

    public ApiServiceOptions ApiService { get; set; } = new();

    public AzuriteOptions Azurite { get; set; } = new();
}

internal sealed class ResourceNameOptions
{
    public string? Storage { get; set; }

    public string? Queues { get; set; }

    public string? Blobs { get; set; }
}

internal sealed class PgAdminOptions
{
    public bool Enabled { get; set; }

    public string? DefaultEmail { get; set; }

    public int HostPort { get; set; }

    public string? Image { get; set; }
}

internal sealed class PostgresOptions
{
    public string? ResourceName { get; set; }

    public string? Host { get; set; }

    public int Port { get; set; }

    public int HostPort { get; set; }

    public string? Username { get; set; }

    public string? PasswordSecretName { get; set; }

    public string? DatabaseName { get; set; }
}

internal sealed class ApiServiceOptions
{
    public int HttpsHostPort { get; set; }

    public int HttpHostPort { get; set; }
}

internal sealed class AzuriteOptions
{
    public int BlobPort { get; set; }

    public int QueuePort { get; set; }

    public int TablePort { get; set; }
}

internal static class AppHostOptionsExtensions
{
    public static AppHostOptions GetRequiredAppHostOptions(this IConfiguration configuration)
    {
        var options = new AppHostOptions();
        configuration.Bind(options);

        var failures = Validate(options)
            .Where(failure => failure.Length > 0)
            .ToArray();
        return failures.Length == 0
            ? options
            : throw new InvalidOperationException(
                "The AppHost configuration is invalid: " + string.Join("; ", failures));
    }

    private static IEnumerable<string> Validate(AppHostOptions options)
    {
        yield return Require(options.Resources.Storage, "Resources:Storage");
        yield return Require(options.Resources.Queues, "Resources:Queues");
        yield return Require(options.Resources.Blobs, "Resources:Blobs");
        yield return Require(options.PgAdmin.DefaultEmail, "PgAdmin:DefaultEmail");
        yield return RequirePositive(options.PgAdmin.HostPort, "PgAdmin:HostPort");
        yield return Require(options.PgAdmin.Image, "PgAdmin:Image");
        yield return Require(options.Postgres.ResourceName, "Postgres:ResourceName");
        yield return Require(options.Postgres.Host, "Postgres:Host");
        yield return RequirePositive(options.Postgres.Port, "Postgres:Port");
        yield return RequirePositive(options.Postgres.HostPort, "Postgres:HostPort");
        yield return Require(options.Postgres.Username, "Postgres:Username");
        yield return Require(options.Postgres.PasswordSecretName, "Postgres:PasswordSecretName");
        yield return Require(options.Postgres.DatabaseName, "Postgres:DatabaseName");
        yield return RequirePositive(options.ApiService.HttpsHostPort, "ApiService:HttpsHostPort");
        yield return RequirePositive(options.ApiService.HttpHostPort, "ApiService:HttpHostPort");
        yield return RequirePositive(options.Azurite.BlobPort, "Azurite:BlobPort");
        yield return RequirePositive(options.Azurite.QueuePort, "Azurite:QueuePort");
        yield return RequirePositive(options.Azurite.TablePort, "Azurite:TablePort");
    }

    private static string Require(string? value, string key)
    {
        return string.IsNullOrWhiteSpace(value)
            ? $"{key} is required"
            : string.Empty;
    }

    private static string RequirePositive(int value, string key)
    {
        return value > 0
            ? string.Empty
            : $"{key} must be greater than 0";
    }
}
