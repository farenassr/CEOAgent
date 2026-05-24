using CEOAgent.Application.Errors;
using CEOAgent.Application.Company;
using CEOAgent.ApiService.Infrastructure.Json;
using CEOAgent.Infrastructure.Persistence;
using CEOAgent.Infrastructure.Entities;
using CEOAgent.Infrastructure.Entities.JsonDocuments;
using CEOAgent.Shared.Request.Company;
using CEOAgent.Shared.Response.Company;
using FastEndpoints;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using CEOAgent.Infrastructure;

namespace CEOAgent.ApiService.Modules.Admin.Companies.Endpoints;

/// <summary>
/// Creates or updates the company's agent profile.
/// </summary>
public sealed class ConfigureAgentProfileEndpoint(
    CEOAgentDbContext dbContext,
    ICompanyContext companyContext) : Endpoint<AgentProfileRequest, CreatedResourceResponse>
{
    public override void Configure()
    {
        Post("/v1/admin/companies/{companyId}/agent-profile");
    }

    public override async Task HandleAsync(AgentProfileRequest req, CancellationToken ct)
    {
        var companyId = Route<Guid>("companyId");
        var company = await GetAccessibleCompanyAsync(dbContext, companyContext, companyId, ct);

        var profile = await dbContext.AgentProfiles
            .WithDefaultTracking(trackChanges: true)
            .FirstOrDefaultAsync(
            entity => entity.CompanyId == companyId,
            ct);

        if (profile is null)
        {
            profile = new AgentProfile
            {
                CompanyId = companyId,
                ModelName = req.ModelName,
                DisplayName = req.DisplayName,
                Language = req.Language,
            };
            dbContext.AgentProfiles.Add(profile);
        }

        profile.ModelName = req.ModelName;
        profile.DisplayName = req.DisplayName;
        profile.Language = req.Language;
        profile.PromptOverride = req.PromptOverride;
        company.TimeZoneId = req.TimeZoneId;
        company.WorkingHours = req.WorkingHours.DeserializeOptional<WorkingHours>();

        await dbContext.SaveChangesAsync(ct);

        await Send.OkAsync(new CreatedResourceResponse(profile.Id), ct);
    }

    private static async Task<CEOAgent.Infrastructure.Entities.Company> GetAccessibleCompanyAsync(
        CEOAgentDbContext dbContext,
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
        RuleFor(request => request.DisplayName).NotEmpty().MaximumLength(160);
        RuleFor(request => request.Language).NotEmpty().MaximumLength(16);
        RuleFor(request => request.TimeZoneId).NotEmpty().MaximumLength(120);
    }
}
