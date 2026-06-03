using CeoAgent.Application.Errors;
using CeoAgent.Application.Company.Abstractions;
using CeoAgent.Application.Company.Implementation;
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
/// Creates or updates the company's agent profile.
/// </summary>
public sealed class ConfigureAgentProfileEndpoint(
    CeoAgentDbContext dbContext,
    ICompanyContext companyContext) : Endpoint<AgentProfileRequest, AgentProfileResponse>
{
    public override void Configure()
    {
        Post("/v1/admin/companies/{companyId}/agent-profile");
    }

    public override async Task HandleAsync(AgentProfileRequest request, CancellationToken cancellationToken)
    {
        var companyId = Route<Guid>("companyId");
        var company = await GetAccessibleCompanyAsync(dbContext, companyContext, companyId, cancellationToken);

        var profile = await dbContext.AgentProfiles
            .WithDefaultTracking(trackChanges: true)
            .FirstOrDefaultAsync(
            entity => entity.CompanyId == companyId,
            cancellationToken);

        if (profile is null)
        {
            profile = CompanyMapper.ToEntity(request, companyId);
            dbContext.AgentProfiles.Add(profile);
        }

        CompanyMapper.ApplyToEntity(request, profile, company);

        await dbContext.SaveChangesAsync(cancellationToken);

        await Send.OkAsync(CompanyMapper.ToResponse(profile), cancellationToken);
    }

    private static async Task<CeoAgent.Infrastructure.Entities.Company> GetAccessibleCompanyAsync(
        CeoAgentDbContext dbContext,
        ICompanyContext companyContext,
        Guid companyId,
        CancellationToken cancellationToken)
    {
        var company = await dbContext.Companies
            .WithDefaultTracking(trackChanges: true)
            .FirstOrDefaultAsync(entity => entity.Id == companyId, cancellationToken);

        if (companyContext.CompanyId != companyId || company is null)
        {
            throw new NotFoundException("company", companyId);
        }

        return company;
    }
}

public sealed class AgentProfileValidator : Validator<AgentProfileRequest>
{
    public AgentProfileValidator()
    {
        RuleFor(request => request.ModelName).NotEmpty().MaximumLength(120);
        RuleFor(request => request.LlmProvider).IsInEnum();
        RuleFor(request => request.DisplayName).NotEmpty().MaximumLength(160);
        RuleFor(request => request.Language).NotEmpty().MaximumLength(16);
        RuleFor(request => request.TimeZoneId).NotEmpty().MaximumLength(120);
    }
}
