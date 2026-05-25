using CeoAgent.Application.Errors;
using CeoAgent.Application.Company;
using CeoAgent.Infrastructure.Persistence;
using CeoAgent.Shared.Request.Company;
using CeoAgent.Shared.Response.Company;
using FastEndpoints;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using CeoAgent.Infrastructure;
using CeoAgent.ApiService.Modules.Companies.Mappers;

namespace CeoAgent.ApiService.Modules.Companies.Endpoints;

/// <summary>
/// Registers an external integration credential reference for a company.
/// </summary>
public sealed class RegisterIntegrationCredentialEndpoint(
    CeoAgentDbContext dbContext,
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
        CeoAgentDbContext dbContext,
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
