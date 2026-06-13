namespace CeoAgent.ApiService.Infrastructure.Security;

public sealed class KeycloakOptions
{
    public const string SectionName = "Keycloak";
    public const string DefaultApiClientId = "ceo-agent-api";
    public const string DefaultServiceClientId = "ceo-agent-service";

    public string ClientId { get; set; } = DefaultApiClientId;

    public string ServiceClientId { get; set; } = DefaultServiceClientId;

    public string Issuer { get; set; } = string.Empty;

    public string RedirectUri { get; set; } = string.Empty;

    public string[] AuthorizationScopes { get; set; } =
    [
        "openid",
        "profile",
        "email",
        "organization",
    ];

    public string ServiceClientSecret { get; set; } = string.Empty;

    public static bool IsValid(KeycloakOptions options)
    {
        return !string.IsNullOrWhiteSpace(options.ClientId)
            && !string.IsNullOrWhiteSpace(options.ServiceClientId)
            && options.AuthorizationScopes.Length > 0
            && options.AuthorizationScopes.All(scope => !string.IsNullOrWhiteSpace(scope))
            && Uri.TryCreate(options.Issuer, UriKind.Absolute, out _);
    }
}
