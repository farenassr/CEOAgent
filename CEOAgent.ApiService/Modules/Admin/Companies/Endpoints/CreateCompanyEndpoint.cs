using CEOAgent.ApiService.Infrastructure.Auth;
using CEOAgent.ApiService.Modules.Admin.Companies.Models.Request;
using CEOAgent.ApiService.Modules.Admin.Companies.Models.Response;
using CEOAgent.Infrastructure.Persistence;
using CEOAgent.Infrastructure.Persistence.Entities;
using FastEndpoints;
using FluentValidation;

namespace CEOAgent.ApiService.Modules.Admin.Companies.Endpoints;

/// <summary>
/// Creates a company for manual platform onboarding.
/// </summary>
public sealed class CreateCompanyEndpoint(AppDbContext dbContext) : Endpoint<CreateCompanyRequest, CompanyResponse>
{
    public override void Configure()
    {
        Post("/v1/admin/companies");
        AuthSchemes(AdminApiKeyAuthenticationDefaults.AuthenticationScheme);
    }

    public override async Task HandleAsync(CreateCompanyRequest req, CancellationToken ct)
    {
        var company = new Company
        {
            Name = req.Name,
            TimeZoneId = req.TimeZoneId
        };

        dbContext.Companies.Add(company);
        await dbContext.SaveChangesAsync(ct);

        var response = new CompanyResponse(company.Id, company.Name, company.Status.ToString());
        await Send.CreatedAtAsync<CreateCompanyEndpoint>(new { response.Id }, response, cancellation: ct);
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
