using CeoAgent.ApiService.Infrastructure.Security;
using CeoAgent.Shared.Request.Company;
using CeoAgent.Shared.Response.Company;
using FastEndpoints;
using FluentValidation;
using CeoAgent.Infrastructure;
using CeoAgent.ApiService.Modules.Companies.Mappers;
using CeoAgent.Shared.Security;
using System.Text.Json;

namespace CeoAgent.ApiService.Modules.Companies.Endpoints;

/// <summary>
/// Registers an external integration credential reference for a company.
/// </summary>
public sealed class RegisterIntegrationCredentialEndpoint(
    CeoAgentDbContext dbContext,
    IAdminTenantGuard tenantGuard) : Endpoint<IntegrationCredentialRequest, IntegrationCredentialResponse>
{
    public override void Configure()
    {
        Post("/v1/admin/companies/{organizationId}/integration-credentials");
    }

    public override async Task HandleAsync(IntegrationCredentialRequest request, CancellationToken cancellationToken)
    {
        var organizationId = Route<Guid>("organizationId");
        await tenantGuard.GetAccessibleCompanyAsync(organizationId, trackChanges: false, cancellationToken);

        var credential = CompanyMapper.ToEntity(request, organizationId);

        dbContext.IntegrationCredentialReferences.Add(credential);
        await dbContext.SaveChangesAsync(cancellationToken);

        await Send.OkAsync(CompanyMapper.ToResponse(credential), cancellationToken);
    }
}

public sealed class IntegrationCredentialValidator : Validator<IntegrationCredentialRequest>
{
    public IntegrationCredentialValidator()
    {
        RuleFor(request => request.Provider).IsInEnum();
        RuleFor(request => request.Purpose).NotEmpty().MaximumLength(80);
        RuleFor(request => request.Reference)
            .NotEmpty()
            .MaximumLength(300)
            .Must(CredentialReference.IsSupportedSecretReference)
            .WithMessage("Credential reference must be a kv:// alias, config:// key, or Azure Key Vault secret URI.");
        RuleFor(request => request.Metadata)
            .Must(NotContainCredentialMaterial)
            .WithMessage("Credential metadata must not contain credential material.");
    }

    private static bool NotContainCredentialMaterial(JsonElement? metadata)
    {
        if (metadata is not { ValueKind: not JsonValueKind.Null and not JsonValueKind.Undefined } value)
        {
            return true;
        }

        return !ContainsCredentialMaterial(value);
    }

    private static bool ContainsCredentialMaterial(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                if (IsCredentialMaterialProperty(property.Name)
                    || ContainsCredentialMaterial(property.Value))
                {
                    return true;
                }
            }
        }

        if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                if (ContainsCredentialMaterial(item))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool IsCredentialMaterialProperty(string propertyName)
    {
        return propertyName.Equals("private_key", StringComparison.OrdinalIgnoreCase)
            || propertyName.Equals("privateKey", StringComparison.OrdinalIgnoreCase)
            || propertyName.Equals("private_key_id", StringComparison.OrdinalIgnoreCase)
            || propertyName.Equals("client_email", StringComparison.OrdinalIgnoreCase)
            || propertyName.Equals("client_id", StringComparison.OrdinalIgnoreCase)
            || propertyName.Equals("service_account_json", StringComparison.OrdinalIgnoreCase)
            || propertyName.Equals("access_token", StringComparison.OrdinalIgnoreCase)
            || propertyName.Equals("refresh_token", StringComparison.OrdinalIgnoreCase)
            || propertyName.Equals("token", StringComparison.OrdinalIgnoreCase)
            || propertyName.Equals("secret", StringComparison.OrdinalIgnoreCase);
    }
}
