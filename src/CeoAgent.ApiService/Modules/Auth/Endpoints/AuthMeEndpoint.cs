using CeoAgent.Application.Abstractions.Organization;
using CeoAgent.Shared.Response;
using FastEndpoints;

namespace CeoAgent.ApiService.Modules.Auth.Endpoints;

public sealed class AuthMeEndpoint(
    IOrganizationContextProvider organizationContext) : EndpointWithoutRequest<AuthMeResponse>
{
    public override void Configure()
    {
        Get("/v1/auth/me");
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
