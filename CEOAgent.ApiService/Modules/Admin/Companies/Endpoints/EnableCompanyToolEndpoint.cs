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
/// Enables or disables a tool for a company.
/// </summary>
public sealed class EnableCompanyToolEndpoint(
    CEOAgentDbContext dbContext,
    ICompanyContext companyContext) : Endpoint<CompanyToolRequest, CreatedResourceResponse>
{
    public override void Configure()
    {
        Post("/v1/admin/companies/{companyId}/tools");
        AuthSchemes(AdminApiKeyAuthenticationDefaults.AuthenticationScheme);
    }

    public override async Task HandleAsync(CompanyToolRequest req, CancellationToken ct)
    {
        var companyId = Route<Guid>("companyId");
        await EnsureCompanyIsAccessibleAsync(dbContext, companyContext, companyId, ct);
        await EnsureCredentialReferenceIsAccessibleAsync(dbContext, req.CredentialReferenceId, ct);

        var tool = await dbContext.CompanyTools.SingleOrDefaultAsync(
            entity => entity.CompanyId == companyId && entity.ToolKey == req.ToolKey,
            ct);

        if (tool is null)
        {
            tool = new CompanyTool
            {
                CompanyId = companyId,
                ToolKey = req.ToolKey
            };
            dbContext.CompanyTools.Add(tool);
        }

        tool.IsEnabled = req.IsEnabled;
        tool.CredentialReferenceId = req.CredentialReferenceId;
        tool.Configuration = req.Configuration;

        await dbContext.SaveChangesAsync(ct);

        await Send.OkAsync(new CreatedResourceResponse(tool.Id), ct);
    }

    private static async Task EnsureCompanyIsAccessibleAsync(
        CEOAgentDbContext dbContext,
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

    private static async Task EnsureCredentialReferenceIsAccessibleAsync(
        CEOAgentDbContext dbContext,
        Guid? credentialReferenceId,
        CancellationToken cancellationToken)
    {
        if (credentialReferenceId is { } id
            && !await dbContext.IntegrationCredentialReferences.AnyAsync(entity => entity.Id == id, cancellationToken))
        {
            throw new NotFoundException("integration_credential_reference", id);
        }
    }
}

public sealed class CompanyToolValidator : Validator<CompanyToolRequest>
{
    public CompanyToolValidator()
    {
        RuleFor(request => request.ToolKey).NotEmpty().MaximumLength(120);
    }
}
