using CeoAgent.Application.Abstractions.AITools;
using CeoAgent.Infrastructure.Persistence.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;

namespace CeoAgent.Infrastructure.Implementation.AI;

internal sealed class AgentFunctionCatalog(
    CeoAgentDbContext dbContext,
    IAgentToolCatalog toolCatalog)
{
    public async Task<IList<AITool>> GetEnabledFunctionsAsync(
        Guid organizationId,
        CancellationToken cancellationToken)
    {
        var tools = await toolCatalog.GetToolsAsync(
            new AgentToolCatalogContext(organizationId),
            cancellationToken);
        var toolsByKey = tools.ToDictionary(tool => tool.ToolKey, StringComparer.Ordinal);

        var enabledTools = await dbContext.CompanyTools
            .AsNoTracking()
            .EnabledForOrganization(organizationId)
            .OrderBy(entity => entity.ToolKey)
            .Select(entity => new
            {
                entity.Id,
                entity.ToolKey,
                entity.Description,
                entity.ParametersSchema,
            })
            .ToArrayAsync(cancellationToken);

        return [.. enabledTools
            .Where(entity => toolsByKey.ContainsKey(entity.ToolKey))
            .Select(entity => (AITool)new AgentToolAIFunction(
                toolsByKey[entity.ToolKey],
                entity.Id,
                entity.ToolKey,
                entity.Description ?? toolsByKey[entity.ToolKey].Description,
                (entity.ParametersSchema ?? toolsByKey[entity.ToolKey].ParametersSchema).Clone(),
                toolsByKey[entity.ToolKey].IsMutating))];
    }
}
