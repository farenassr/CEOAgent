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
/// Registers a provider channel for company resolution.
/// </summary>
public sealed class RegisterCompanyChannelEndpoint(
    AppDbContext dbContext,
    ICompanyContext companyContext) : Endpoint<CompanyChannelRequest, CreatedResourceResponse>
{
    public override void Configure()
    {
        Post("/v1/admin/companies/{companyId}/channels");
        AuthSchemes(AdminApiKeyAuthenticationDefaults.AuthenticationScheme);
    }

    public override async Task HandleAsync(CompanyChannelRequest req, CancellationToken ct)
    {
        var companyId = Route<Guid>("companyId");
        await EnsureCompanyIsAccessibleAsync(dbContext, companyContext, companyId, ct);
        await EnsureCredentialReferenceIsAccessibleAsync(dbContext, req.CredentialReferenceId, ct);

        var channel = new CompanyChannel
        {
            CompanyId = companyId,
            Provider = req.Provider,
            ProviderChannelId = req.ProviderChannelId,
            Metadata = req.Metadata,
            CredentialReferenceId = req.CredentialReferenceId
        };

        dbContext.CompanyChannels.Add(channel);
        await dbContext.SaveChangesAsync(ct);

        await Send.OkAsync(new CreatedResourceResponse(channel.Id), ct);
    }

    private static async Task EnsureCompanyIsAccessibleAsync(
        AppDbContext dbContext,
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
        AppDbContext dbContext,
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

public sealed class CompanyChannelValidator : Validator<CompanyChannelRequest>
{
    public CompanyChannelValidator()
    {
        RuleFor(request => request.Provider).NotEmpty().MaximumLength(80);
        RuleFor(request => request.ProviderChannelId).NotEmpty().MaximumLength(160);
    }
}
