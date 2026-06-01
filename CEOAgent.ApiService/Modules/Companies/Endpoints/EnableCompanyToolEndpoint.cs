using CeoAgent.Application.Errors;
using CeoAgent.Application.Company;
using CeoAgent.Infrastructure.Persistence;
using CeoAgent.Shared.Request.Company;
using CeoAgent.Shared.Response.Company;
using FastEndpoints;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using CeoAgent.Infrastructure;
using CeoAgent.Infrastructure.Entities.JsonDocuments;
using CeoAgent.Infrastructure.Scheduling;
using CeoAgent.ApiService.Modules.Companies.Mappers;

namespace CeoAgent.ApiService.Modules.Companies.Endpoints;

/// <summary>
/// Enables or disables a tool for a company.
/// </summary>
public sealed class EnableCompanyToolEndpoint(
    CeoAgentDbContext dbContext,
    ICompanyContext companyContext) : Endpoint<CompanyToolRequest, CompanyToolResponse>
{
    public override void Configure()
    {
        Post("/v1/admin/companies/{companyId}/tools");
    }

    public override async Task HandleAsync(CompanyToolRequest request, CancellationToken cancellationToken)
    {
        var companyId = Route<Guid>("companyId");
        await EnsureCompanyIsAccessibleAsync(dbContext, companyContext, companyId, cancellationToken);
        await EnsureCredentialReferenceIsAccessibleAsync(dbContext, request.CredentialReferenceId, cancellationToken);

        var tool = await dbContext.CompanyTools
            .WithDefaultTracking(trackChanges: true)
            .FirstOrDefaultAsync(
            entity => entity.CompanyId == companyId && entity.ToolKey == request.ToolKey,
            cancellationToken);

        if (tool is null)
        {
            tool = CompanyMapper.ToEntity(request, companyId);
            dbContext.CompanyTools.Add(tool);
        }

        CompanyMapper.ApplyToEntity(request, tool);
        ValidateConfiguration(tool.Configuration);

        await dbContext.SaveChangesAsync(cancellationToken);

        await Send.OkAsync(CompanyMapper.ToResponse(tool), cancellationToken);
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

    private static async Task EnsureCredentialReferenceIsAccessibleAsync(
        CeoAgentDbContext dbContext,
        Guid? credentialReferenceId,
        CancellationToken cancellationToken)
    {
        if (credentialReferenceId is { } id
            && !await dbContext.IntegrationCredentialReferences
                .WithDefaultTracking()
                .AnyAsync(entity => entity.Id == id, cancellationToken))
        {
            throw new NotFoundException("integration_credential_reference", id);
        }
    }

    private static void ValidateConfiguration(ToolConfiguration? configuration)
    {
        if (configuration?.GoogleCalendar is { } googleCalendar)
        {
            GoogleCalendarConfigValidator.Validate(googleCalendar);
        }
    }
}

public sealed class CompanyToolValidator : Validator<CompanyToolRequest>
{
    public CompanyToolValidator()
    {
        RuleFor(request => request.ToolKey).NotEmpty().MaximumLength(120);
        RuleFor(request => request.Description).MaximumLength(1000);
    }
}
