using System.Text.Json;
using CeoAgent.Infrastructure;
using CeoAgent.Integrations.AI;
using CeoAgent.Shared.Constants;
using Microsoft.EntityFrameworkCore;

namespace CeoAgent.Tools.Implementation.Execution;

public sealed class CompanyToolRegistry(CeoAgentDbContext dbContext)
{
    public async Task<IReadOnlyList<AgentToolDescriptor>> GetEnabledToolsAsync(
        Guid companyId,
        CancellationToken cancellationToken)
    {
        var tools = await dbContext.CompanyTools
            .AsNoTracking()
            .Where(entity => entity.CompanyId == companyId && entity.IsEnabled)
            .OrderBy(entity => entity.ToolKey)
            .Select(entity => new
            {
                entity.Id,
                entity.ToolKey,
                entity.Description,
                entity.ParametersSchema,
            })
            .ToArrayAsync(cancellationToken);

        return tools
            .Where(tool => !string.IsNullOrWhiteSpace(tool.Description) && tool.ParametersSchema is not null)
            .Select(tool => new AgentToolDescriptor(
                tool.Id,
                tool.ToolKey,
                tool.Description!.Trim(),
                tool.ParametersSchema!.Value.Clone(),
                IsMutating(tool.ToolKey)))
            .ToArray();
    }

    private static bool IsMutating(string toolKey)
    {
        return string.Equals(toolKey, MvpToolKeys.CreateGoogleCalendarReservation, StringComparison.Ordinal);
    }

}
