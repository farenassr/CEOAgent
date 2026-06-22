using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using CeoAgent.Application.Abstractions.Secrets;
using Google.GenAI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;

namespace CeoAgent.Infrastructure.Implementation.Gemini;

internal sealed class GeminiChatClientFactory(
    ISecretValueProvider secrets,
    IOptions<GeminiAgentRuntimeOptions> options) : IGeminiChatClientFactory
{
    private readonly ConcurrentDictionary<string, IChatClient> clients = new(StringComparer.Ordinal);

    public async Task<IChatClient> GetClientAsync(string modelName, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(modelName);

        if (string.IsNullOrWhiteSpace(options.Value.ApiKeyReference))
        {
            throw new InvalidOperationException("LlmProviders:Gemini:ApiKeyReference is required.");
        }

        var apiKey = await secrets.GetSecretValueAsync(options.Value.ApiKeyReference, cancellationToken);
        var cacheKey = Convert.ToHexString(SHA256.HashData(
            Encoding.UTF8.GetBytes($"{options.Value.ApiKeyReference}:{modelName}")));

        return clients.GetOrAdd(cacheKey, _ =>
            new Client(apiKey: apiKey).AsIChatClient(modelName));
    }
}
