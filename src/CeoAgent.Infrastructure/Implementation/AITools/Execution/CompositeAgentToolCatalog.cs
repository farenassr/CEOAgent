using CeoAgent.Application.Abstractions.AITools;

namespace CeoAgent.Infrastructure.Implementation.AITools.Execution;

public sealed class CompositeAgentToolCatalog : IAgentToolCatalog
{
    private readonly IReadOnlyList<IAgentTool> staticTools;
    private readonly IReadOnlyList<IDynamicAgentToolProvider> dynamicProviders;
    private readonly Dictionary<string, IAgentTool> staticToolsByKey;

    public CompositeAgentToolCatalog(
        IEnumerable<IAgentTool> staticTools,
        IEnumerable<IDynamicAgentToolProvider> dynamicProviders)
    {
        this.staticTools = staticTools.ToArray();
        this.dynamicProviders = dynamicProviders.ToArray();
        staticToolsByKey = BuildStaticToolMap(this.staticTools);
    }

    public async Task<IReadOnlyList<IAgentTool>> GetToolsAsync(
        AgentToolCatalogContext context,
        CancellationToken cancellationToken)
    {
        var tools = new List<IAgentTool>(staticTools);
        var dynamicToolKeys = new HashSet<string>(StringComparer.Ordinal);

        foreach (var provider in dynamicProviders)
        {
            var dynamicTools = await provider.GetToolsAsync(context, cancellationToken);
            foreach (var dynamicTool in dynamicTools)
            {
                if (staticToolsByKey.ContainsKey(dynamicTool.ToolKey))
                {
                    throw new InvalidOperationException(
                        $"Dynamic agent tool '{dynamicTool.ToolKey}' conflicts with a static tool key.");
                }

                if (!dynamicToolKeys.Add(dynamicTool.ToolKey))
                {
                    throw new InvalidOperationException(
                        $"Dynamic agent tool key '{dynamicTool.ToolKey}' is registered more than once.");
                }

                tools.Add(dynamicTool);
            }
        }

        return tools;
    }

    private static Dictionary<string, IAgentTool> BuildStaticToolMap(
        IReadOnlyList<IAgentTool> tools)
    {
        var duplicates = tools
            .GroupBy(tool => tool.ToolKey, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToArray();
        if (duplicates.Length > 0)
        {
            throw new InvalidOperationException(
                $"Static agent tool key '{duplicates[0]}' is registered more than once.");
        }

        return tools.ToDictionary(tool => tool.ToolKey, StringComparer.Ordinal);
    }
}

public sealed class NoOpDynamicAgentToolProvider : IDynamicAgentToolProvider
{
    public Task<IReadOnlyList<IAgentTool>> GetToolsAsync(
        AgentToolCatalogContext context,
        CancellationToken cancellationToken)
    {
        return Task.FromResult<IReadOnlyList<IAgentTool>>([]);
    }
}
