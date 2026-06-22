using CeoAgent.Infrastructure.Implementation.AI;
using Microsoft.Extensions.DependencyInjection;

namespace CeoAgent.Infrastructure.Implementation.Gemini;

internal static class GeminiImplementationRegistrations
{
    public static IServiceCollection AddGeminiImplementation(this IServiceCollection services)
    {
        services.AddSingleton<IGeminiChatClientFactory, GeminiChatClientFactory>();
        services.AddScoped<IAgentRuntimeProvider, GeminiAgentRuntime>();
        return services;
    }
}
