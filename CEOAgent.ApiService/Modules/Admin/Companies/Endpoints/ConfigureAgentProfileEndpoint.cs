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
/// Creates or updates the company's agent profile.
/// </summary>
public sealed class ConfigureAgentProfileEndpoint(
    AppDbContext dbContext,
    ICompanyContext companyContext) : Endpoint<AgentProfileRequest, CreatedResourceResponse>
{
    public override void Configure()
    {
        Post("/v1/admin/companies/{companyId}/agent-profile");
        AuthSchemes(AdminApiKeyAuthenticationDefaults.AuthenticationScheme);
    }

    public override async Task HandleAsync(AgentProfileRequest req, CancellationToken ct)
    {
        var companyId = Route<Guid>("companyId");
        var company = await GetAccessibleCompanyAsync(dbContext, companyContext, companyId, ct);

        var profile = await dbContext.AgentProfiles.SingleOrDefaultAsync(
            entity => entity.CompanyId == companyId,
            ct);

        if (profile is null)
        {
            profile = new AgentProfile
            {
                CompanyId = companyId,
                ModelName = req.ModelName,
                DisplayName = req.DisplayName,
                Language = req.Language
            };
            dbContext.AgentProfiles.Add(profile);
        }

        profile.ModelName = req.ModelName;
        profile.DisplayName = req.DisplayName;
        profile.Language = req.Language;
        profile.PromptOverride = req.PromptOverride;
        company.TimeZoneId = req.TimeZoneId;
        company.WorkingHoursJson = req.WorkingHoursJson;

        await dbContext.SaveChangesAsync(ct);

        await Send.OkAsync(new CreatedResourceResponse(profile.Id), ct);
    }

    private static async Task<CEOAgent.Infrastructure.Persistence.Entities.Company> GetAccessibleCompanyAsync(
        AppDbContext dbContext,
        ICompanyContext companyContext,
        Guid companyId,
        CancellationToken cancellationToken)
    {
        var company = await dbContext.Companies.SingleOrDefaultAsync(entity => entity.Id == companyId, cancellationToken);

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
