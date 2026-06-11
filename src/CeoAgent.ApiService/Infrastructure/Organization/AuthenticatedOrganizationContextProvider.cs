namespace CeoAgent.ApiService.Infrastructure.Organization;

public sealed class AuthenticatedOrganizationContextProvider
{
    public bool TryGetOrganizationId(HttpContext context, out Guid organizationId)
    {
        return KeycloakOrganizationClaimParser.TryGetOrganizationId(context.User, out organizationId);
    }
}
