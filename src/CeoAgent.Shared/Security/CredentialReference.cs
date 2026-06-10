namespace CeoAgent.Shared.Security;

/// <summary>
/// Validates supported secret reference formats without resolving the secret value.
/// </summary>
public sealed record CredentialReference(string Value, CredentialReferenceKind Kind)
{
    public const string ConfigScheme = "config://";
    public const string KeyVaultAliasScheme = "kv://";

    public static bool IsSupportedSecretReference(string? reference)
    {
        return TryParse(reference, out _);
    }

    public static bool TryParse(string? reference, out CredentialReference? credentialReference)
    {
        credentialReference = null;
        if (string.IsNullOrWhiteSpace(reference))
        {
            return false;
        }

        var trimmed = reference.Trim();
        if (trimmed.StartsWith(ConfigScheme, StringComparison.OrdinalIgnoreCase))
        {
            return TryCreateConfiguredReference(trimmed, ConfigScheme, CredentialReferenceKind.Configuration, out credentialReference);
        }

        if (trimmed.StartsWith(KeyVaultAliasScheme, StringComparison.OrdinalIgnoreCase))
        {
            return TryCreateConfiguredReference(trimmed, KeyVaultAliasScheme, CredentialReferenceKind.KeyVaultAlias, out credentialReference);
        }

        if (!Uri.TryCreate(trimmed, UriKind.Absolute, out var secretUri)
            || !string.Equals(secretUri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)
            || string.Equals(secretUri.Host, "vault.azure.net", StringComparison.OrdinalIgnoreCase)
            || !secretUri.Host.EndsWith(".vault.azure.net", StringComparison.OrdinalIgnoreCase)
            || !string.IsNullOrEmpty(secretUri.Query)
            || !string.IsNullOrEmpty(secretUri.Fragment))
        {
            return false;
        }

        var pathSegments = secretUri.AbsolutePath.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (pathSegments.Length is < 2 or > 3
            || !string.Equals(pathSegments[0], "secrets", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        credentialReference = new CredentialReference(trimmed, CredentialReferenceKind.AzureKeyVaultSecretUri);
        return true;
    }

    private static bool TryCreateConfiguredReference(
        string reference,
        string scheme,
        CredentialReferenceKind kind,
        out CredentialReference? credentialReference)
    {
        credentialReference = null;
        var path = reference[scheme.Length..];
        var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (segments.Length == 0)
        {
            return false;
        }

        credentialReference = new CredentialReference(reference, kind);
        return true;
    }
}

public enum CredentialReferenceKind
{
    Configuration,
    KeyVaultAlias,
    AzureKeyVaultSecretUri,
}
