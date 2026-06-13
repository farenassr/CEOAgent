using CeoAgent.Application.Abstractions.Organization;
using CeoAgent.ApiService.Infrastructure.OpenApi;
using CeoAgent.Shared.Response;
using FastEndpoints;

namespace CeoAgent.ApiService.Modules.Auth.Endpoints;

public sealed class AuthMeEndpoint(
    IOrganizationContextProvider organizationContext) : EndpointWithoutRequest<AuthMeResponse>
{
    public override void Configure()
    {
        Get("/v1/auth/me");
        Description(builder => builder
            .WithTags(OpenApiConstants.Tags.Authentication)
            .WithSummary("Get Authenticated User")
            .WithDescription("Returns the organization identity resolved from the current authenticated request. Use it after login to confirm which company context the API will use."));
        Summary(summary =>
        {
            summary.Summary = "Get Authenticated User";
            summary.Description = "Returns the organization identity resolved from the current authenticated request. Use it after login to confirm which company context the API will use.";
        });
    }

    public override async Task HandleAsync(CancellationToken cancellationToken)
    {
        if (organizationContext.OrganizationId is not { } organizationId)
        {
            await Send.ForbiddenAsync(cancellationToken);
            return;
        }

        await Send.OkAsync(new AuthMeResponse(organizationId), cancellationToken);
    }
}
