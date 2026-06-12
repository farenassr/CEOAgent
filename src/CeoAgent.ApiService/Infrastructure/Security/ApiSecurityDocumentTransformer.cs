using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi;

namespace CeoAgent.ApiService.Infrastructure.Security;

internal sealed class ApiSecurityDocumentTransformer(
    IAuthenticationSchemeProvider authenticationSchemeProvider,
    IOptions<KeycloakOptions> keycloakOptions) : IOpenApiDocumentTransformer
{
    public async Task TransformAsync(
        OpenApiDocument document,
        OpenApiDocumentTransformerContext context,
        CancellationToken cancellationToken)
    {
        var authenticationSchemes = await authenticationSchemeProvider.GetAllSchemesAsync();
        if (!authenticationSchemes.Any(scheme => scheme.Name == JwtBearerDefaults.AuthenticationScheme))
        {
            return;
        }

        var configuredKeycloakOptions = keycloakOptions.Value;
        if (!KeycloakOptions.HasRequiredAuthorizationSettings(configuredKeycloakOptions))
        {
            return;
        }

        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
        document.Components.SecuritySchemes[OpenApiSecuritySchemeNames.KeycloakOAuth] = CreateKeycloakOAuthScheme(configuredKeycloakOptions);
    }

    private static OpenApiSecurityScheme CreateKeycloakOAuthScheme(KeycloakOptions keycloakOptions)
    {
        var keycloakIssuer = keycloakOptions.Issuer.TrimEnd('/');

        return new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.OAuth2,
            Description = "Sign in with Keycloak using authorization code flow with PKCE.",
            Flows = new OpenApiOAuthFlows
            {
                AuthorizationCode = new OpenApiOAuthFlow
                {
                    AuthorizationUrl = new Uri($"{keycloakIssuer}/protocol/openid-connect/auth"),
                    TokenUrl = new Uri($"{keycloakIssuer}/protocol/openid-connect/token"),
                    Scopes = keycloakOptions.GetConfiguredScopes()
                        .ToDictionary(
                            configuredScope => configuredScope,
                            keycloakOptions.GetConfiguredScopeDescription,
                            StringComparer.Ordinal),
                },
            },
        };
    }
}
