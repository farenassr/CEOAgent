using System.Text.Json;
using CeoAgent.Application.Abstractions.AITools;
using CeoAgent.Infrastructure;
using CeoAgent.Infrastructure.Persistence.Extensions;
using CeoAgent.Application.Abstractions.AI;
using CeoAgent.Shared.AI;
using Microsoft.EntityFrameworkCore;

namespace CeoAgent.Infrastructure.Implementation.AITools.Execution;

public sealed class CompanyToolRegistry(
    CeoAgentDbContext dbContext,
    IAgentToolCatalog catalog)
{
    public async Task<IReadOnlyList<AgentToolDescriptor>> GetEnabledToolsAsync(
        Guid companyId,
        CancellationToken cancellationToken)
    {
        var catalogTools = await catalog.GetToolsAsync(
            new AgentToolCatalogContext(companyId),
            cancellationToken);
        var catalogToolsByKey = catalogTools.ToDictionary(tool => tool.ToolKey, StringComparer.Ordinal);

        var tools = await dbContext.CompanyTools
            .AsNoTracking()
            .EnabledForCompany(companyId)
            .Select(entity => new
            {
                entity.Id,
                entity.ToolKey,
            })
            .ToArrayAsync(cancellationToken);

        return tools
            .Where(tool => catalogToolsByKey.ContainsKey(tool.ToolKey))
            .Select(tool => new AgentToolDescriptor(
                tool.Id,
                tool.ToolKey,
                catalogToolsByKey[tool.ToolKey].Description,
                catalogToolsByKey[tool.ToolKey].ParametersSchema.Clone(),
                catalogToolsByKey[tool.ToolKey].IsMutating))
            .ToArray();
    }
}
