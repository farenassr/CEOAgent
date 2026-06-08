using System.Text.Json;
using CeoAgent.Infrastructure;
using CeoAgent.Infrastructure.Persistence.Extensions;
using CeoAgent.Application.Abstractions.AI;
using CeoAgent.Shared.AI;
using CeoAgent.Shared.Constants;
using Microsoft.EntityFrameworkCore;

namespace CeoAgent.Infrastructure.Implementation.AITools.Execution;

public sealed class CompanyToolRegistry(CeoAgentDbContext dbContext)
{
    public async Task<IReadOnlyList<AgentToolDescriptor>> GetEnabledToolsAsync(
        Guid companyId,
        CancellationToken cancellationToken)
    {
        var tools = await dbContext.CompanyTools
            .AsNoTracking()
            .EnabledForCompany(companyId)
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
        return toolKey is MvpToolKeys.CreateGoogleCalendarReservation
            or MvpToolKeys.UpdateGoogleCalendarReservation
            or MvpToolKeys.CancelGoogleCalendarReservation
            or MvpToolKeys.RequestHumanHandoff;
    }

}
