using CeoAgent.Application.Errors;
using CeoAgent.Application.Abstractions.AITools;
using CeoAgent.ApiService.Infrastructure.Security;
using CeoAgent.ApiService.Infrastructure.OpenApi;
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
        Post("/v1/admin/companies/{organizationId}/tools");
        Description(builder => builder
            .WithTags(OpenApiConstants.Tags.Tools)
            .WithSummary("Configure Company Tool")
            .WithDescription("Enables or updates an agent tool for a company with validated configuration and credential references. Use it to expose business capabilities to the agent runtime."));
        Summary(summary =>
        {
            summary.Summary = "Configure Company Tool";
            summary.Description = "Enables or updates an agent tool for a company with validated configuration and credential references. Use it to expose business capabilities to the agent runtime.";
        });
    }

    public override async Task HandleAsync(CompanyToolRequest request, CancellationToken cancellationToken)
    {
        var organizationId = Route<Guid>("organizationId");
        await tenantGuard.GetAccessibleCompanyAsync(organizationId, trackChanges: false, cancellationToken);
        await tenantGuard.EnsureCredentialReferenceAccessibleAsync(organizationId, request.CredentialReferenceId, cancellationToken);
        var catalogTool = await ResolveCatalogToolAsync(agentToolCatalog, organizationId, request.ToolKey, cancellationToken);

        var tool = await dbContext.CompanyTools
            .WithDefaultTracking(trackChanges: true)
            .FirstOrDefaultAsync(
            entity => entity.OrganizationId == organizationId && entity.ToolKey == request.ToolKey,
            cancellationToken);

        if (tool is null)
        {
            tool = CompanyMapper.ToEntity(request, organizationId);
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
        Guid organizationId,
        string toolKey,
        CancellationToken cancellationToken)
    {
        var tools = await catalog.GetToolsAsync(new AgentToolCatalogContext(organizationId), cancellationToken);
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
