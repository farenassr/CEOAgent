using CeoAgent.Application.Abstractions.AITools;
using CeoAgent.Infrastructure.Persistence.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using System.Text.Json;

namespace CeoAgent.Infrastructure.Implementation.AI;

internal sealed class AgentFunctionCatalog(
    CeoAgentDbContext dbContext,
    IAgentToolCatalog toolCatalog)
{
    public async Task<IList<AITool>> GetEnabledFunctionsAsync(
        Guid organizationId,
        CancellationToken cancellationToken,
        Func<JsonElement, JsonElement>? schemaTransform = null)
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
            .Select(entity =>
            {
                var tool = toolsByKey[entity.ToolKey];
                var schema = (entity.ParametersSchema ?? tool.ParametersSchema).Clone();
                if (schemaTransform is not null)
                {
                    schema = schemaTransform(schema);
                }

                return (AITool)new AgentToolAIFunction(
                    tool,
                    entity.Id,
                    entity.ToolKey,
                    entity.Description ?? tool.Description,
                    schema,
                    tool.IsMutating);
            })];
    }
}
