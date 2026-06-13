using CeoAgent.ApiService.Infrastructure.Security;
using CeoAgent.ApiService.Infrastructure.OpenApi;
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
    IAdminTenantGuard tenantGuard) : Endpoint<AgentProfileRequest, AgentProfileResponse>
{
    public override void Configure()
    {
        Post("/v1/admin/companies/{organizationId}/agent-profile");
        Description(builder => builder
            .WithTags(OpenApiConstants.Tags.AgentProfile)
            .WithSummary("Configure Agent Profile")
            .WithDescription("Creates or updates the company agent profile used by runtime conversations. Use it to set model, display name, language, timezone, and operating policy metadata for a company."));
        Summary(summary =>
        {
            summary.Summary = "Configure Agent Profile";
            summary.Description = "Creates or updates the company agent profile used by runtime conversations. Use it to set model, display name, language, timezone, and operating policy metadata for a company.";
        });
    }

    public override async Task HandleAsync(AgentProfileRequest request, CancellationToken cancellationToken)
    {
        var organizationId = Route<Guid>("organizationId");
        var company = await tenantGuard.GetAccessibleCompanyAsync(organizationId, trackChanges: true, cancellationToken);

        var profile = await dbContext.AgentProfiles
            .WithDefaultTracking(trackChanges: true)
            .FirstOrDefaultAsync(
            entity => entity.OrganizationId == organizationId,
            cancellationToken);

        if (profile is null)
        {
            profile = CompanyMapper.ToEntity(request, organizationId);
            dbContext.AgentProfiles.Add(profile);
        }

        CompanyMapper.ApplyToEntity(request, profile, company);

        await dbContext.SaveChangesAsync(cancellationToken);

        await Send.OkAsync(CompanyMapper.ToResponse(profile), cancellationToken);
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
