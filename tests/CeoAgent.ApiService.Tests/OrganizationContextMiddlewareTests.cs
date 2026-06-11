using CeoAgent.ApiService.Infrastructure.Organization;
using CeoAgent.Infrastructure.Implementation.Organization;
using Microsoft.AspNetCore.Http;
using Shouldly;
using System.Security.Claims;

namespace CeoAgent.ApiService.Tests;

public sealed class OrganizationContextMiddlewareTests
{
    private static readonly Guid OrganizationId = Guid.Parse("b36cfb51-83bd-4376-b7d7-0502141ff6ae");

    [Test]
    public async Task InvokeAsync_WhenPublicRequestHasNoOrganizationClaim_DoesNotSetOrganizationContext()
    {
        var accessor = new OrganizationContextAccessor();
        var observedOrganizationId = Guid.NewGuid();
        var middleware = new OrganizationContextMiddleware(context =>
        {
            observedOrganizationId = accessor.OrganizationId ?? Guid.Empty;
            return Task.CompletedTask;
        });
        var context = new DefaultHttpContext();
        context.Request.Path = "/v1/whatsapp";

        await middleware.InvokeAsync(
            context,
            accessor,
            new AuthenticatedOrganizationContextProvider());

        observedOrganizationId.ShouldBe(Guid.Empty);
        accessor.OrganizationId.ShouldBeNull();
    }

    [Test]
    public async Task InvokeAsync_WhenJwtContainsOrganizationClaim_SetsOrganizationContextForRequestOnly()
    {
        var accessor = new OrganizationContextAccessor();
        var observedOrganizationId = Guid.Empty;
        var middleware = new OrganizationContextMiddleware(context =>
        {
            observedOrganizationId = accessor.OrganizationId ?? Guid.Empty;
            return Task.CompletedTask;
        });
        var context = new DefaultHttpContext();
        context.Request.Path = "/v1/admin/companies";
        context.User = CreatePrincipal($$"""
            {
              "la-terraza-org": {
                "id": "{{OrganizationId:D}}"
              }
            }
            """);

        await middleware.InvokeAsync(
            context,
            accessor,
            new AuthenticatedOrganizationContextProvider());

        observedOrganizationId.ShouldBe(OrganizationId);
        accessor.OrganizationId.ShouldBeNull();
    }

    [Test]
    public async Task InvokeAsync_WhenProtectedRequestHasInvalidOrganizationClaim_ReturnsForbiddenWithoutCallingNext()
    {
        var accessor = new OrganizationContextAccessor();
        var nextCalled = false;
        var middleware = new OrganizationContextMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });
        var context = new DefaultHttpContext();
        context.Request.Path = "/v1/admin/companies";
        context.User = CreatePrincipal("""{"la-terraza-org":{"id":"not-a-guid"}}""");

        await middleware.InvokeAsync(
            context,
            accessor,
            new AuthenticatedOrganizationContextProvider());

        context.Response.StatusCode.ShouldBe(StatusCodes.Status403Forbidden);
        nextCalled.ShouldBeFalse();
        accessor.OrganizationId.ShouldBeNull();
    }

    [Test]
    public async Task InvokeAsync_WhenProtectedRequestHasNoOrganizationClaim_ReturnsForbiddenWithoutCallingNext()
    {
        var accessor = new OrganizationContextAccessor();
        var nextCalled = false;
        var middleware = new OrganizationContextMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });
        var context = new DefaultHttpContext();
        context.Request.Path = "/v1/admin/companies";
        context.User = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, "dev-user")],
            authenticationType: "Bearer"));

        await middleware.InvokeAsync(
            context,
            accessor,
            new AuthenticatedOrganizationContextProvider());

        context.Response.StatusCode.ShouldBe(StatusCodes.Status403Forbidden);
        nextCalled.ShouldBeFalse();
        accessor.OrganizationId.ShouldBeNull();
    }

    private static ClaimsPrincipal CreatePrincipal(string organizationClaim)
    {
        return new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, "test-user"),
                new Claim("organization", organizationClaim),
            ],
            authenticationType: "Bearer"));
    }
}
