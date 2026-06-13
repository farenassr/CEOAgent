using CeoAgent.ApiService.Infrastructure.OpenApi;
using CeoAgent.Shared.Request.Company;
using CeoAgent.Shared.Response.Company;
using FastEndpoints;
using FluentValidation;
using CeoAgent.Infrastructure;
using CeoAgent.ApiService.Modules.Companies.Mappers;
using CeoAgent.Application.Abstractions.Organization;

namespace CeoAgent.ApiService.Modules.Companies.Endpoints;

/// <summary>
/// Creates a company for manual platform onboarding.
/// </summary>
public sealed class CreateCompanyEndpoint(
    CeoAgentDbContext dbContext,
    IOrganizationContextProvider organizationContext) : Endpoint<CreateCompanyRequest, CompanyResponse>
{
    public override void Configure()
    {
        Post("/v1/admin/companies");
        Description(builder => builder
            .WithTags(OpenApiConstants.Tags.Companies)
            .WithSummary("Create Company")
            .WithDescription("Creates a company record for manual onboarding in the authenticated organization context. Use this before configuring channels, credentials, tools, or agent profile data."));
        Summary(summary =>
        {
            summary.Summary = "Create Company";
            summary.Description = "Creates a company record for manual onboarding in the authenticated organization context. Use this before configuring channels, credentials, tools, or agent profile data.";
        });
    }

    public override async Task HandleAsync(CreateCompanyRequest request, CancellationToken cancellationToken)
    {
        if (organizationContext.OrganizationId is not { } organizationId)
        {
            await Send.ForbiddenAsync(cancellationToken);
            return;
        }

        var company = CompanyMapper.ToEntity(request, organizationId);

        dbContext.Companies.Add(company);
        await dbContext.SaveChangesAsync(cancellationToken);

        var response = CompanyMapper.ToResponse(company);
        await Send.CreatedAtAsync<CreateCompanyEndpoint>(new { response.Id }, response, cancellation: cancellationToken);
    }
}

public sealed class CreateCompanyValidator : Validator<CreateCompanyRequest>
{
    public CreateCompanyValidator()
    {
        RuleFor(request => request.Name)
            .NotEmpty()
            .MaximumLength(200);
        RuleFor(request => request.TimeZoneId)
            .NotEmpty()
            .MaximumLength(120);
    }
}
