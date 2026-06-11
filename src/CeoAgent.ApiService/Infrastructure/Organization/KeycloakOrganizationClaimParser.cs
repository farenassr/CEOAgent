using System.Security.Claims;
using System.Text.Json;

namespace CeoAgent.ApiService.Infrastructure.Organization;

public static class KeycloakOrganizationClaimParser
{
    public const string ClaimType = "organization";

    public static bool TryGetOrganizationId(ClaimsPrincipal principal, out Guid organizationId)
    {
        organizationId = default;
        var organizationClaim = principal.FindFirst(ClaimType)?.Value;
        if (string.IsNullOrWhiteSpace(organizationClaim))
        {
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(organizationClaim);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return false;
            }

            foreach (var organizationProperty in document.RootElement.EnumerateObject())
            {
                if (organizationProperty.Value.ValueKind != JsonValueKind.Object
                    || !organizationProperty.Value.TryGetProperty("id", out var idProperty)
                    || idProperty.ValueKind != JsonValueKind.String)
                {
                    continue;
                }

                return Guid.TryParse(idProperty.GetString(), out organizationId);
            }

            return false;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
