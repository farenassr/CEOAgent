using Microsoft.AspNetCore.Authentication;

namespace CEOAgent.ApiService.Infrastructure.Auth;

public sealed class AdminApiKeyOptions : AuthenticationSchemeOptions
{
    public string? ApiKey { get; set; }
}
