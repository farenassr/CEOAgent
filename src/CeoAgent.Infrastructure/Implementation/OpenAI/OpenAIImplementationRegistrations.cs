using CeoAgent.Application.Abstractions.AI;
using CeoAgent.Application.Abstractions.OpenAI;
using Microsoft.Extensions.DependencyInjection;
using OpenAI.Responses;

namespace CeoAgent.Infrastructure.Implementation.OpenAI;

#pragma warning disable OPENAI001

internal static class OpenAIImplementationRegistrations
{
    public static IServiceCollection AddOpenAIImplementation(this IServiceCollection services)
    {
        services.AddSingleton<IOpenAIResponsesClientFactory<ResponsesClient>, OpenAIResponsesClientFactory>();
        services.AddScoped<IAgentRuntime, OpenAIAgentRuntime>();
        return services;
    }
}

#pragma warning restore OPENAI001
