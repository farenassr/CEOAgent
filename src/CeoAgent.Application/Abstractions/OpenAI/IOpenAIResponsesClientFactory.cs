namespace CeoAgent.Application.Abstractions.OpenAI;

public interface IOpenAIResponsesClientFactory<TClient>
{
    Task<TClient> GetClientAsync(CancellationToken cancellationToken);
}
