using CeoAgent.ApiService.Tests.Support;
using Shouldly;
using System.Net;
using System.Net.Http.Json;

namespace CeoAgent.ApiService.Tests;

public sealed class AuthMeEndpointTests
{
    [Test]
    public async Task AuthMe_WithOrganizationClaim_ReturnsOrganizationId()
    {
        await using var factory = new ApiFactory();
        var organizationId = Guid.Parse("b36cfb51-83bd-4376-b7d7-0502141ff6ae");
        using var client = factory.CreateAuthenticatedClient(organizationId);

        var response = await client.GetAsync("/v1/auth/me");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<AuthMeResponse>();
        body.ShouldNotBeNull();
        body.OrganizationId.ShouldBe(organizationId);
    }

    private sealed class AuthMeResponse
    {
        public Guid OrganizationId { get; set; }
    }
}
