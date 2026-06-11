using CeoAgent.ApiService.Infrastructure.Organization;
using Shouldly;
using System.Security.Claims;

namespace CeoAgent.ApiService.Tests;

public sealed class KeycloakOrganizationClaimParserTests
{
    [Test]
    public void TryGetOrganizationId_WhenOrganizationClaimContainsNestedId_ReturnsOrganizationId()
    {
        var organizationId = Guid.Parse("b36cfb51-83bd-4376-b7d7-0502141ff6ae");
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim("organization", $$"""
                    {
                      "la-terraza-org": {
                        "id": "{{organizationId:D}}"
                      }
                    }
                    """),
            ],
            authenticationType: "Bearer"));

        var found = KeycloakOrganizationClaimParser.TryGetOrganizationId(principal, out var parsedOrganizationId);

        found.ShouldBeTrue();
        parsedOrganizationId.ShouldBe(organizationId);
    }

    [Test]
    public void TryGetOrganizationId_WhenOrganizationClaimIsMissing_ReturnsFalse()
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity(authenticationType: "Bearer"));

        var found = KeycloakOrganizationClaimParser.TryGetOrganizationId(principal, out var parsedOrganizationId);

        found.ShouldBeFalse();
        parsedOrganizationId.ShouldBe(Guid.Empty);
    }

    [Test]
    public void TryGetOrganizationId_WhenNestedIdIsNotGuid_ReturnsFalse()
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim("organization", """{"la-terraza-org":{"id":"not-a-guid"}}""")],
            authenticationType: "Bearer"));

        var found = KeycloakOrganizationClaimParser.TryGetOrganizationId(principal, out var parsedOrganizationId);

        found.ShouldBeFalse();
        parsedOrganizationId.ShouldBe(Guid.Empty);
    }
}
