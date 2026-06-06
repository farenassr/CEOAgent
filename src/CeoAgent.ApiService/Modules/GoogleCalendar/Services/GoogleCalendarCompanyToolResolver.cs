using CeoAgent.Application.Abstractions.Company;
using CeoAgent.Infrastructure.Implementation.Company;
using CeoAgent.Application.Errors;
using CeoAgent.Infrastructure;
using CeoAgent.Infrastructure.Entities;
using CeoAgent.Infrastructure.Entities.JsonDocuments;
using CeoAgent.Infrastructure.Implementation.AITools.GoogleCalendar.Integration;
using CeoAgent.Infrastructure.Persistence;
using CeoAgent.Infrastructure.Implementation.AITools.GoogleCalendar;
using Microsoft.EntityFrameworkCore;

namespace CeoAgent.ApiService.Modules.GoogleCalendar;

public sealed class GoogleCalendarCompanyToolResolver(
    CeoAgentDbContext dbContext,
    ICompanyContext companyContext)
{
    public async Task<GoogleCalendarCompanyToolContext> ResolveAsync(
        Guid companyId,
        string toolKey,
        CancellationToken cancellationToken)
    {
        var company = await dbContext.Companies
            .WithDefaultTracking()
            .FirstOrDefaultAsync(entity => entity.Id == companyId, cancellationToken);

        if (companyContext.CompanyId != companyId || company is null)
        {
            throw new NotFoundException("company", companyId);
        }

        var tool = await dbContext.CompanyTools
            .Include(entity => entity.CredentialReference)
            .WithDefaultTracking()
            .FirstOrDefaultAsync(
                entity => entity.CompanyId == companyId
                    && entity.ToolKey == toolKey
                    && entity.IsEnabled,
                cancellationToken);

        if (tool is null)
        {
            throw NotConfigured();
        }

        var configuration = tool.Configuration?.GoogleCalendar;
        var credentialReference = tool.CredentialReference;
        if (configuration is null
            || credentialReference is null)
        {
            throw NotConfigured();
        }

        GoogleCalendarConfigValidator.Validate(configuration);
        var credential = GoogleCalendarCredentialMaterialResolver.Resolve(credentialReference);

        return new GoogleCalendarCompanyToolContext(
            company,
            tool,
            configuration,
            credential);
    }

    private static BusinessRuleException NotConfigured()
    {
        return new BusinessRuleException(
            "google_calendar_tool_not_configured",
            "Google Calendar tool, configuration, and credential reference are required.");
    }
}

public sealed record GoogleCalendarCompanyToolContext(
    Company Company,
    CompanyTool Tool,
    GoogleCalendarConfig Configuration,
    string CredentialReference);
