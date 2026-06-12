namespace CeoAgent.ApiService.Infrastructure.Security;

public sealed class KeycloakOptions
{
    public const string SectionName = "Keycloak";

    public string ClientId { get; set; } = string.Empty;

    public string Issuer { get; set; } = string.Empty;

    public string[] Scopes { get; set; } = [];

    public Dictionary<string, string> ScopeDescriptions { get; set; } = [];

    public static bool HasRequiredAuthorizationSettings(KeycloakOptions keycloakOptions)
    {
        return !string.IsNullOrWhiteSpace(keycloakOptions.ClientId)
            && Uri.TryCreate(keycloakOptions.Issuer, UriKind.Absolute, out _)
            && keycloakOptions.GetConfiguredScopes().Count > 0;
    }

    public IReadOnlyList<string> GetConfiguredScopes()
    {
        return Scopes
            .Where(configuredScope => !string.IsNullOrWhiteSpace(configuredScope))
            .Select(configuredScope => configuredScope.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    public string GetConfiguredScopeDescription(string configuredScope)
    {
        return ScopeDescriptions.TryGetValue(configuredScope, out var configuredScopeDescription)
            && !string.IsNullOrWhiteSpace(configuredScopeDescription)
                ? configuredScopeDescription
                : configuredScope;
    }
}
