using CEOAgent.Application.Errors;
using CEOAgent.Application.Company;
using CEOAgent.Infrastructure.Persistence;
using CEOAgent.Shared.Request.Company;
using CEOAgent.Shared.Response.Company;
using FastEndpoints;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using CEOAgent.Infrastructure;
using CEOAgent.ApiService.Modules.Companies.Mappers;

namespace CEOAgent.ApiService.Modules.Companies.Endpoints;

/// <summary>
/// Registers an external integration credential reference for a company.
/// </summary>
public sealed class RegisterIntegrationCredentialEndpoint(
    CEOAgentDbContext dbContext,
    ICompanyContext companyContext) : Endpoint<IntegrationCredentialRequest, IntegrationCredentialResponse>
{
    public override void Configure()
    {
        Post("/v1/admin/companies/{companyId}/integration-credentials");
    }

    public override async Task HandleAsync(IntegrationCredentialRequest request, CancellationToken cancellationToken)
    {
        var companyId = Route<Guid>("companyId");
        await EnsureCompanyIsAccessibleAsync(dbContext, companyContext, companyId, cancellationToken);

        var credential = CompanyMapper.ToEntity(request, companyId);

        dbContext.IntegrationCredentialReferences.Add(credential);
        await dbContext.SaveChangesAsync(cancellationToken);

        await Send.OkAsync(CompanyMapper.ToResponse(credential), cancellationToken);
    }

    private static async Task EnsureCompanyIsAccessibleAsync(
        CEOAgentDbContext dbContext,
        ICompanyContext companyContext,
        Guid companyId,
        CancellationToken cancellationToken)
    {
        if (companyContext.CompanyId != companyId
            || !await dbContext.Companies
                .WithDefaultTracking()
                .AnyAsync(entity => entity.Id == companyId, cancellationToken))
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
