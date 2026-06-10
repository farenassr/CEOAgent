using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using CeoAgent.Application.Abstractions.Secrets;
using CeoAgent.Shared.Security;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using System.Collections.Concurrent;

namespace CeoAgent.Infrastructure.Implementation.Secrets;

/// <summary>
/// Resolves secret references from local configuration or Azure Key Vault with short-lived in-memory caching.
/// </summary>
public sealed class SecretValueProvider : ISecretValueProvider
{
    private static readonly DefaultAzureCredential Credential = new();
    private static readonly ConcurrentDictionary<Uri, SecretClient> Clients = new();
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5);

    private readonly IConfiguration configuration;
    private readonly IMemoryCache cache;

    public SecretValueProvider(IConfiguration configuration, IMemoryCache cache)
    {
        this.configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        this.cache = cache ?? throw new ArgumentNullException(nameof(cache));
    }

    /// <summary>
    /// Returns the secret value for a config reference or Key Vault URI, caching remote secret reads.
    /// </summary>
    public async Task<string> GetSecretValueAsync(
        string reference,
        CancellationToken cancellationToken)
    {
        if (!CredentialReference.TryParse(reference, out var credentialReference)
            || credentialReference is null)
        {
            throw new InvalidOperationException("Credential reference must be a kv:// alias, config:// key, or Azure Key Vault secret URI.");
        }

        if (credentialReference.Kind == CredentialReferenceKind.Configuration)
        {
            return GetConfiguredSecret(credentialReference.Value[CredentialReference.ConfigScheme.Length..]);
        }

        if (credentialReference.Kind == CredentialReferenceKind.KeyVaultAlias)
        {
            return GetConfiguredSecret(ToKeyVaultAliasConfigurationKey(credentialReference.Value[CredentialReference.KeyVaultAliasScheme.Length..]));
        }

        if (cache.TryGetValue(credentialReference.Value, out string? cachedValue)
            && cachedValue is not null)
        {
            return cachedValue;
        }

        var value = await GetKeyVaultSecretValueAsync(credentialReference.Value, cancellationToken);
        cache.Set(credentialReference.Value, value, CacheDuration);
        return value;
    }

    private string GetConfiguredSecret(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new InvalidOperationException("Configured secret reference requires a configuration key.");
        }

        var value = configuration[key];
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"Configured secret '{key}' is missing.");
        }

        return value;
    }

    private static async Task<string> GetKeyVaultSecretValueAsync(
        string reference,
        CancellationToken cancellationToken)
    {
        if (!Uri.TryCreate(reference, UriKind.Absolute, out var secretUri))
        {
            throw new InvalidOperationException("Credential reference must be a kv:// alias, config:// key, or Azure Key Vault secret URI.");
        }

        var pathSegments = secretUri.AbsolutePath.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (pathSegments.Length < 2 || !string.Equals(pathSegments[0], "secrets", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Credential reference must point to a Key Vault secret.");
        }

        var vaultUri = new UriBuilder(secretUri)
        {
            Path = string.Empty,
            Query = string.Empty,
            Fragment = string.Empty,
        }.Uri;

        var clientOptions = new SecretClientOptions
        {
            Retry =
            {
                NetworkTimeout = TimeSpan.FromSeconds(30),
                MaxRetries = 3,
                Delay = TimeSpan.FromSeconds(1),
                MaxDelay = TimeSpan.FromSeconds(10),
                Mode = Azure.Core.RetryMode.Exponential
            }
        };
        var client = Clients.GetOrAdd(vaultUri, uri => new SecretClient(uri, Credential, clientOptions));
        var secret = pathSegments.Length >= 3
            ? await client.GetSecretAsync(pathSegments[1], pathSegments[2], cancellationToken)
            : await client.GetSecretAsync(pathSegments[1], cancellationToken: cancellationToken);

        return secret.Value.Value;
    }

    private static string ToKeyVaultAliasConfigurationKey(string aliasPath)
    {
        var segments = aliasPath
            .Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (segments.Length == 0)
        {
            throw new InvalidOperationException("Key Vault alias reference requires a non-empty path.");
        }

        return "Secrets:" + string.Join(':', segments);
    }
}
