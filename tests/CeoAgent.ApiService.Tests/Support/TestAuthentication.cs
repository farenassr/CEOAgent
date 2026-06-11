using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CeoAgent.ApiService.Tests.Support;

internal static class TestAuthentication
{
    public const string Scheme = "TestBearer";

    public static AuthenticationHeaderValue MissingOrganizationBearer()
    {
        return new AuthenticationHeaderValue("Bearer", "missing-organization");
    }

    public static AuthenticationHeaderValue OrganizationBearer(Guid organizationId)
    {
        return new AuthenticationHeaderValue("Bearer", $"organization:{organizationId:N}");
    }
}

internal sealed class TestAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue("Authorization", out var authorization)
            || !AuthenticationHeaderValue.TryParse(authorization, out var header)
            || !string.Equals(header.Scheme, "Bearer", StringComparison.OrdinalIgnoreCase)
            || string.IsNullOrWhiteSpace(header.Parameter))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, "test-user"),
        };

        if (header.Parameter.StartsWith("organization:", StringComparison.OrdinalIgnoreCase)
            && Guid.TryParse(header.Parameter["organization:".Length..], out var organizationId))
        {
            claims.Add(new Claim(
                "organization",
                $"{{\"la-terraza-org\":{{\"id\":\"{organizationId:D}\"}}}}"));
        }

        var identity = new ClaimsIdentity(claims, TestAuthentication.Scheme);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, TestAuthentication.Scheme);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
