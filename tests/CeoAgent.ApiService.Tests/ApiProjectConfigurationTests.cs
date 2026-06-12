using System.Xml.Linq;
using System.Text.Json;
using CeoAgent.ApiService.Tests.Support;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Scalar.AspNetCore;
using Shouldly;

namespace CeoAgent.ApiService.Tests;

public sealed class ApiProjectConfigurationTests
{
    [Test]
    public void ApiServiceProject_DefinesUserSecretsIdForLocalGoogleCalendarCredentials()
    {
        var projectPath = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "..",
            "src",
            "CeoAgent.ApiService",
            "CeoAgent.ApiService.csproj"));
        var document = XDocument.Load(projectPath);

        var userSecretsId = document
            .Descendants("UserSecretsId")
            .SingleOrDefault()
            ?.Value;

        userSecretsId.ShouldNotBeNullOrWhiteSpace();
    }

    [Test]
    public void Program_AppliesAuthenticationBeforeAuthorization()
    {
        var programPath = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "..",
            "src",
            "CeoAgent.ApiService",
            "Program.cs"));
        var program = File.ReadAllText(programPath);

        program.IndexOf("app.UseAuthentication();", StringComparison.Ordinal)
            .ShouldBeLessThan(program.IndexOf("app.UseAuthorization();", StringComparison.Ordinal));
    }

    [Test]
    public void ApiServiceAppsettings_DefinesKeycloakNonSecretSettingsOnly()
    {
        var appsettingsPath = GetRepoFilePath("src", "CeoAgent.ApiService", "appsettings.json");
        using var document = JsonDocument.Parse(File.ReadAllText(appsettingsPath));

        var keycloakConfiguration = document.RootElement.GetProperty("Keycloak");
        keycloakConfiguration.GetProperty("ClientId").GetString().ShouldBe("ceo-agent-api");
        keycloakConfiguration.GetProperty("Issuer").GetString()
            .ShouldBe("https://ceo-agent-keycloak.icybush-34e28ac8.westus2.azurecontainerapps.io/realms/ceo-agent");
        keycloakConfiguration.GetProperty("Scopes")
            .EnumerateArray()
            .Select(scopeElement => scopeElement.GetString())
            .ShouldBe(["openid", "profile", "email", "organization"]);
        keycloakConfiguration.GetProperty("ScopeDescriptions")
            .GetProperty("organization")
            .GetString()
            .ShouldBe("Keycloak organization membership claim");
        keycloakConfiguration.TryGetProperty("ClientSecret", out _).ShouldBeFalse();
        keycloakConfiguration.TryGetProperty("RedirectUri", out _).ShouldBeFalse();
        keycloakConfiguration.TryGetProperty("ServiceClientId", out _).ShouldBeFalse();
        keycloakConfiguration.TryGetProperty("ServiceClientSecret", out _).ShouldBeFalse();
        keycloakConfiguration.TryGetProperty("OAuthClientId", out _).ShouldBeFalse();
    }

    [Test]
    public void AppHostAppsettings_DefinesKeycloakApiClientOnly()
    {
        var appsettingsPath = GetRepoFilePath("src", "CeoAgent.AppHost", "appsettings.json");
        using var document = JsonDocument.Parse(File.ReadAllText(appsettingsPath));

        var keycloakConfiguration = document.RootElement
            .GetProperty("Keycloak");

        keycloakConfiguration
            .GetProperty("ClientId")
            .GetString()
            .ShouldBe("ceo-agent-api");
        keycloakConfiguration
            .GetProperty("Issuer")
            .GetString()
            .ShouldBe("https://ceo-agent-keycloak.icybush-34e28ac8.westus2.azurecontainerapps.io/realms/ceo-agent");
        keycloakConfiguration.GetProperty("Scopes")
            .EnumerateArray()
            .Select(scopeElement => scopeElement.GetString())
            .ShouldBe(["openid", "profile", "email", "organization"]);
        keycloakConfiguration.GetProperty("ScopeDescriptions")
            .GetProperty("openid")
            .GetString()
            .ShouldBe("OpenID Connect sign-in");
        keycloakConfiguration.TryGetProperty("RedirectUri", out _).ShouldBeFalse();
        keycloakConfiguration.TryGetProperty("ServiceClientId", out _).ShouldBeFalse();
    }

    [Test]
    public void AppHostAppsettings_DoesNotStoreKeycloakClientSecret()
    {
        var appsettingsPath = GetRepoFilePath("src", "CeoAgent.AppHost", "appsettings.json");
        using var document = JsonDocument.Parse(File.ReadAllText(appsettingsPath));

        document.RootElement
            .GetProperty("Keycloak")
            .TryGetProperty("ClientSecret", out _)
            .ShouldBeFalse();
        document.RootElement
            .GetProperty("Keycloak")
            .TryGetProperty("ServiceClientSecret", out _)
            .ShouldBeFalse();
    }

    [Test]
    public void AppHost_OnlyPassesKeycloakApiClientSettings()
    {
        var appHostPath = GetRepoFilePath("src", "CeoAgent.AppHost", "AppHost.cs");
        var appHost = File.ReadAllText(appHostPath);

        appHost.ShouldNotContain("""builder.AddParameter("keycloak-client-secret", secret: true)""");
        appHost.ShouldNotContain("""builder.AddParameter("keycloak-client-api-service", secret: true)""");
        appHost.ShouldContain("AddKeycloakEnvironment(builder, apiService);");
        appHost.ShouldNotContain("AddKeycloakEnvironment(builder, worker);");
        appHost.ShouldContain("""builder.Configuration["Keycloak:ClientId"]""");
        appHost.ShouldContain("""builder.Configuration["Keycloak:Issuer"]""");
        appHost.ShouldContain("""builder.Configuration.GetSection("Keycloak:Scopes").Get<string[]>()""");
        appHost.ShouldContain("""GetSection("Keycloak:ScopeDescriptions")""");
        appHost.ShouldContain(""".Get<Dictionary<string, string>>()""");
        appHost.ShouldContain("""Keycloak__ClientId""");
        appHost.ShouldContain("""Keycloak__Issuer""");
        appHost.ShouldContain("""Keycloak__Scopes__{scopeIndex}""");
        appHost.ShouldContain("""Keycloak__ScopeDescriptions__{scopeDescriptionPair.Key}""");
        appHost.ShouldNotContain("""builder.Configuration["Keycloak:ServiceClientId"]""");
        appHost.ShouldNotContain("""Keycloak__ServiceClientId""");
        appHost.ShouldNotContain("""Keycloak__ServiceClientSecret""");
        appHost.ShouldNotContain("""Keycloak__RedirectUri""");
        appHost.ShouldNotContain("Keycloak__ClientSecret");
        appHost.ShouldNotContain("""builder.Configuration["Keycloak:ClientSecret"]""");
    }

    [Test]
    public void ScalarOptions_UsesCeoAgentApiClientForAuthorization()
    {
        using var factory = new ApiFactory(
            "Development",
            settings: new Dictionary<string, string?>
            {
                ["Keycloak:Scopes:0"] = "openid",
                ["Keycloak:Scopes:1"] = "custom-scope",
                ["Keycloak:Scopes:2"] = "",
                ["Keycloak:Scopes:3"] = "",
                ["Keycloak:ScopeDescriptions:custom-scope"] = "Custom configured scope",
            });

        var scalarOptions = factory.Services.GetRequiredService<IOptions<ScalarOptions>>().Value;
        var keycloakSecurityScheme = scalarOptions.Authentication?
            .SecuritySchemes?["KeycloakOAuth"] as ScalarOAuth2SecurityScheme;

        keycloakSecurityScheme.ShouldNotBeNull();
        var authorizationCode = keycloakSecurityScheme.Flows?.AuthorizationCode;
        authorizationCode.ShouldNotBeNull();
        authorizationCode.ClientId.ShouldBe("ceo-agent-api");
        authorizationCode.ClientSecret.ShouldBeNullOrWhiteSpace();
        authorizationCode.Pkce.ShouldBe(Pkce.Sha256);
        authorizationCode.SelectedScopes.ShouldBe(["openid", "custom-scope"]);
        authorizationCode.RedirectUri.ShouldBeNullOrWhiteSpace();
    }

    private static string GetRepoFilePath(params string[] parts)
    {
        return Path.GetFullPath(Path.Combine(
            [
                AppContext.BaseDirectory,
                "..",
                "..",
                "..",
                "..",
                "..",
                .. parts,
            ]));
    }
}
