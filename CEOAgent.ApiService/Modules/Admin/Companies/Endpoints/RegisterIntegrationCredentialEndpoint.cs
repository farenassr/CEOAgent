using CEOAgent.Application.Errors;
using CEOAgent.Application.Company;
using CEOAgent.ApiService.Infrastructure.Auth;
using CEOAgent.ApiService.Modules.Admin.Companies.Models.Request;
using CEOAgent.ApiService.Modules.Admin.Companies.Models.Response;
using CEOAgent.Infrastructure.Persistence;
using CEOAgent.Infrastructure.Persistence.Entities;
using FastEndpoints;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace CEOAgent.ApiService.Modules.Admin.Companies.Endpoints;

/// <summary>
/// Registers an external integration credential reference for a company.
/// </summary>
public sealed class RegisterIntegrationCredentialEndpoint(
    AppDbContext dbContext,
    ICompanyContext companyContext) : Endpoint<IntegrationCredentialRequest, CreatedResourceResponse>
{
    public override void Configure()
    {
        Post("/v1/admin/companies/{companyId}/integration-credentials");
        AuthSchemes(AdminApiKeyAuthenticationDefaults.AuthenticationScheme);
    }

    public override async Task HandleAsync(IntegrationCredentialRequest req, CancellationToken ct)
    {
        var companyId = Route<Guid>("companyId");
        await EnsureCompanyIsAccessibleAsync(dbContext, companyContext, companyId, ct);

        var credential = new IntegrationCredentialReference
        {
            CompanyId = companyId,
            Provider = req.Provider,
            Purpose = req.Purpose,
            Reference = req.Reference,
            MetadataJson = req.MetadataJson
        };

        dbContext.IntegrationCredentialReferences.Add(credential);
        await dbContext.SaveChangesAsync(ct);

        await Send.OkAsync(new CreatedResourceResponse(credential.Id), ct);
    }

    private static async Task EnsureCompanyIsAccessibleAsync(
        AppDbContext dbContext,
        ICompanyContext companyContext,
        Guid companyId,
        CancellationToken cancellationToken)
    {
        if (companyContext.CompanyId != companyId
            || !await dbContext.Companies.AnyAsync(entity => entity.Id == companyId, cancellationToken))
        {
            throw new NotFoundException("company", companyId);
        }
    }
}

public sealed class IntegrationCredentialValidator : Validator<IntegrationCredentialRequest>
{
    public IntegrationCredentialValidator()
    {
        RuleFor(request => request.Provider).NotEmpty().MaximumLength(80);
        RuleFor(request => request.Purpose).NotEmpty().MaximumLength(80);
        RuleFor(request => request.Reference).NotEmpty().MaximumLength(300);
    }
}
