using CeoAgent.Shared.Request.Company;
using CeoAgent.Shared.Response.Company;
using FastEndpoints;
using FluentValidation;
using CeoAgent.Infrastructure;
using CeoAgent.ApiService.Modules.Companies.Mappers;

namespace CeoAgent.ApiService.Modules.Companies.Endpoints;

/// <summary>
/// Creates a company for manual platform onboarding.
/// </summary>
public sealed class CreateCompanyEndpoint(CeoAgentDbContext dbContext) : Endpoint<CreateCompanyRequest, CompanyResponse>
{
    public override void Configure()
    {
        Post("/v1/admin/companies");
    }

    public override async Task HandleAsync(CreateCompanyRequest request, CancellationToken cancellationToken)
    {
        var company = CompanyMapper.ToEntity(request);

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
