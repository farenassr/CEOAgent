using System.Collections.Concurrent;
using CeoAgent.Adapters.Secrets;
using Microsoft.Extensions.Options;
using OpenAI.Responses;

namespace CeoAgent.Adapters.OpenAI;

#pragma warning disable OPENAI001

internal sealed class OpenAIResponsesClientFactory(
    ISecretValueProvider secrets,
    IOptions<OpenAIAgentRuntimeOptions> options) : IOpenAIResponsesClientFactory
{
    private readonly ConcurrentDictionary<string, ResponsesClient> clients = new(StringComparer.Ordinal);

    public async Task<ResponsesClient> GetClientAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(options.Value.ApiKeyReference))
        {
            throw new InvalidOperationException("LlmProviders:OpenAI:ApiKeyReference is required.");
        }

        var apiKey = await secrets.GetSecretValueAsync(options.Value.ApiKeyReference, cancellationToken);
        return clients.GetOrAdd(apiKey, static key => new ResponsesClient(key));
    }
}

#pragma warning restore OPENAI001
