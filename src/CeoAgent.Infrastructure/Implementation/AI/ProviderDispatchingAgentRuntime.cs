using CeoAgent.Application.Abstractions.AI;
using CeoAgent.Shared.AI;
using CeoAgent.Shared.Enums;
using Microsoft.Extensions.Hosting;

namespace CeoAgent.Infrastructure.Implementation.AI;

internal sealed class ProviderDispatchingAgentRuntime(
    IEnumerable<IAgentRuntimeProvider> providers,
    IHostEnvironment environment) : IAgentRuntime
{
    private readonly Dictionary<LlmProvider, IAgentRuntimeProvider> providersByProvider =
        providers.ToDictionary(provider => provider.Provider);

    public bool CanEstimateCost(LlmProvider provider, string modelName)
    {
        return IsProviderAllowed(provider)
            && providersByProvider.TryGetValue(provider, out var runtimeProvider)
            && runtimeProvider.CanEstimateCost(modelName);
    }

    public Task<AgentTurnResult> RunTurnAsync(
        AgentTurnRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        EnsureProviderAllowed(request.Provider);

        if (!providersByProvider.TryGetValue(request.Provider, out var runtimeProvider))
        {
            throw new NotSupportedException($"LLM provider '{request.Provider}' is not supported.");
        }

        return runtimeProvider.RunTurnAsync(request, cancellationToken);
    }

    private bool IsProviderAllowed(LlmProvider provider)
    {
        return provider != LlmProvider.Ollama || IsLocalDevelopmentOrTesting(environment);
    }

    private void EnsureProviderAllowed(LlmProvider provider)
    {
        if (!IsProviderAllowed(provider))
        {
            throw new NotSupportedException("Ollama is only supported in Local, Development, or Testing environments.");
        }
    }

    private static bool IsLocalDevelopmentOrTesting(IHostEnvironment hostEnvironment)
    {
        return hostEnvironment.IsDevelopment()
            || hostEnvironment.IsEnvironment("Local")
            || hostEnvironment.IsEnvironment("Testing");
    }
}
