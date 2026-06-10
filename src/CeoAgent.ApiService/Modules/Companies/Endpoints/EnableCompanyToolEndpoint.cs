using CeoAgent.Application.Errors;
using CeoAgent.Application.Abstractions.AITools;
using CeoAgent.ApiService.Infrastructure.Security;
using CeoAgent.Infrastructure.Persistence;
using CeoAgent.Shared.Request.Company;
using CeoAgent.Shared.Response.Company;
using FastEndpoints;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using CeoAgent.Infrastructure;
using CeoAgent.Infrastructure.Entities.JsonDocuments;
using CeoAgent.Infrastructure.Implementation.AITools.GoogleCalendar;
using CeoAgent.ApiService.Modules.Companies.Mappers;
using System.Text.Json;

namespace CeoAgent.ApiService.Modules.Companies.Endpoints;

/// <summary>
/// Enables or disables a tool for a company.
/// </summary>
public sealed class EnableCompanyToolEndpoint(
    CeoAgentDbContext dbContext,
    IAdminTenantGuard tenantGuard,
    IAgentToolCatalog agentToolCatalog) : Endpoint<CompanyToolRequest, CompanyToolResponse>
{
    public override void Configure()
    {
        Post("/v1/admin/companies/{companyId}/tools");
    }

    public override async Task HandleAsync(CompanyToolRequest request, CancellationToken cancellationToken)
    {
        var companyId = Route<Guid>("companyId");
        await tenantGuard.GetAccessibleCompanyAsync(companyId, trackChanges: false, cancellationToken);
        await tenantGuard.EnsureCredentialReferenceAccessibleAsync(companyId, request.CredentialReferenceId, cancellationToken);
        var catalogTool = await ResolveCatalogToolAsync(agentToolCatalog, companyId, request.ToolKey, cancellationToken);

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
        tool.Description = string.IsNullOrWhiteSpace(request.Description) ? catalogTool.Description : request.Description;
        tool.ParametersSchema = catalogTool.ParametersSchema.Clone();
        ValidateConfiguration(tool.Configuration);

        await dbContext.SaveChangesAsync(cancellationToken);

        await Send.OkAsync(CompanyMapper.ToResponse(tool), cancellationToken);
    }

    private static void ValidateConfiguration(ToolConfiguration? configuration)
    {
        if (configuration?.GoogleCalendar is { } googleCalendar)
        {
            GoogleCalendarConfigValidator.Validate(googleCalendar);
        }
    }

    private static async Task<IAgentTool> ResolveCatalogToolAsync(
        IAgentToolCatalog catalog,
        Guid companyId,
        string toolKey,
        CancellationToken cancellationToken)
    {
        var tools = await catalog.GetToolsAsync(new AgentToolCatalogContext(companyId), cancellationToken);
        return tools.SingleOrDefault(tool => string.Equals(tool.ToolKey, toolKey, StringComparison.Ordinal))
            ?? throw new NotFoundException("agent_tool", toolKey);
    }
}

public sealed class CompanyToolValidator : Validator<CompanyToolRequest>
{
    public CompanyToolValidator()
    {
        RuleFor(request => request.ToolKey).NotEmpty().MaximumLength(120);
        RuleFor(request => request.Description).MaximumLength(1000);
        RuleFor(request => request.ParametersSchema)
            .Must(BeObjectSchema)
            .When(request => request.ParametersSchema is not null)
            .WithMessage("Parameters schema must be a JSON object.");
    }

    private static bool BeObjectSchema(JsonElement? schema)
    {
        return schema is { ValueKind: JsonValueKind.Object };
    }
}
