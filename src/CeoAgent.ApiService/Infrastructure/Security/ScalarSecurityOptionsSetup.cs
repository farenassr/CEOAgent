using Microsoft.Extensions.Options;
using Scalar.AspNetCore;

namespace CeoAgent.ApiService.Infrastructure.Security;

internal sealed class ScalarSecurityOptionsSetup(IOptions<KeycloakOptions> keycloakOptions) : IConfigureOptions<ScalarOptions>
{
    public void Configure(ScalarOptions scalarOptions)
    {
        scalarOptions.AddPreferredSecuritySchemes(OpenApiSecuritySchemeNames.KeycloakOAuth);

        var configuredKeycloakOptions = keycloakOptions.Value;
        if (!KeycloakOptions.HasRequiredAuthorizationSettings(configuredKeycloakOptions))
        {
            return;
        }

        scalarOptions.AddAuthorizationCodeFlow(OpenApiSecuritySchemeNames.KeycloakOAuth, authorizationCodeFlow =>
        {
            authorizationCodeFlow.ClientId = configuredKeycloakOptions.ClientId;
            authorizationCodeFlow.SelectedScopes = configuredKeycloakOptions.GetConfiguredScopes();
            authorizationCodeFlow.Pkce = Pkce.Sha256;
        });
    }
}
