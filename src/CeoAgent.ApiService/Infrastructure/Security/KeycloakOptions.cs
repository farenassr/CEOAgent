namespace CeoAgent.ApiService.Infrastructure.Security;

public sealed class KeycloakOptions
{
    public const string SectionName = "Keycloak";

    public string ClientId { get; set; } = string.Empty;

    public string Issuer { get; set; } = string.Empty;

    public string RedirectUri { get; set; } = string.Empty;

    public string ClientSecret { get; set; } = string.Empty;

    public static bool IsValid(KeycloakOptions options)
    {
        return !string.IsNullOrWhiteSpace(options.ClientId)
            && Uri.TryCreate(options.Issuer, UriKind.Absolute, out _);
    }
}
