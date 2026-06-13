using CeoAgent.ApiService.Infrastructure.Security;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi;

namespace CeoAgent.ApiService.Infrastructure.OpenApi;

internal sealed class KeycloakOpenApiDocumentTransformer(
    IOptions<KeycloakOptions> keycloakOptions) : IOpenApiDocumentTransformer
{
    public Task TransformAsync(
        OpenApiDocument document,
        OpenApiDocumentTransformerContext context,
        CancellationToken cancellationToken)
    {
        document.Info ??= new OpenApiInfo();
        document.Info.Title = "CeoAgent API Reference";

        var issuer = keycloakOptions.Value.Issuer.TrimEnd('/');
        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
        document.Components.SecuritySchemes[OpenApiConstants.KeycloakOAuthScheme] = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.OAuth2,
            Description = "Authenticate with Keycloak using OAuth2 authorization code flow with PKCE.",
            Flows = new OpenApiOAuthFlows
            {
                AuthorizationCode = new OpenApiOAuthFlow
                {
                    AuthorizationUrl = new Uri($"{issuer}/protocol/openid-connect/auth", UriKind.Absolute),
                    TokenUrl = new Uri($"{issuer}/protocol/openid-connect/token", UriKind.Absolute),
                    Scopes = new Dictionary<string, string>
                    {
                        ["openid"] = "Authenticate the user.",
                        ["profile"] = "Read user profile claims.",
                        ["email"] = "Read user email claims.",
                        ["organization"] = "Read user organization claims.",
                    },
                },
            },
        };

        return Task.CompletedTask;
    }
}
