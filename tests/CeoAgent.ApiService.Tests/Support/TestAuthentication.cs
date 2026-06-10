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

    public static AuthenticationHeaderValue BootstrapBearer()
    {
        return new AuthenticationHeaderValue("Bearer", "bootstrap");
    }

    public static AuthenticationHeaderValue CompanyBearer(Guid companyId)
    {
        return new AuthenticationHeaderValue("Bearer", $"company:{companyId:N}");
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

        if (header.Parameter.StartsWith("company:", StringComparison.OrdinalIgnoreCase)
            && Guid.TryParse(header.Parameter["company:".Length..], out var companyId))
        {
            claims.Add(new Claim("company_id", companyId.ToString()));
        }

        var identity = new ClaimsIdentity(claims, TestAuthentication.Scheme);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, TestAuthentication.Scheme);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
