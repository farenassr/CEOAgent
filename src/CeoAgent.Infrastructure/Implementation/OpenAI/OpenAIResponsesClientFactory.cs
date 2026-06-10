using System.Collections.Concurrent;
using CeoAgent.Application.Abstractions.OpenAI;
using CeoAgent.Application.Abstractions.Secrets;
using Microsoft.Extensions.Options;
using OpenAI.Responses;

namespace CeoAgent.Infrastructure.Implementation.OpenAI;

#pragma warning disable OPENAI001

internal sealed class OpenAIResponsesClientFactory(
    ISecretValueProvider secrets,
    IOptions<OpenAIAgentRuntimeOptions> options) : IOpenAIResponsesClientFactory<ResponsesClient>
{
    private readonly ConcurrentDictionary<string, ResponsesClient> clients = new(StringComparer.Ordinal);

    public async Task<ResponsesClient> GetClientAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(options.Value.ApiKeyReference))
        {
            throw new InvalidOperationException("LlmProviders:OpenAI:ApiKeyReference is required.");
        }

        var apiKey = await secrets.GetSecretValueAsync(options.Value.ApiKeyReference, cancellationToken);
        var cacheKey = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(options.Value.ApiKeyReference)));
        return clients.GetOrAdd(cacheKey, _ =>
        {
            var clientOptions = new global::OpenAI.OpenAIClientOptions
            {
                NetworkTimeout = TimeSpan.FromSeconds(30)
            };
            return new ResponsesClient(new System.ClientModel.ApiKeyCredential(apiKey), clientOptions);
        });
    }
}

#pragma warning restore OPENAI001
