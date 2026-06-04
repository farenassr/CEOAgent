using OpenAI.Responses;

namespace CeoAgent.Adapters.OpenAI;

#pragma warning disable OPENAI001

public interface IOpenAIResponsesClientFactory
{
    Task<ResponsesClient> GetClientAsync(CancellationToken cancellationToken);
}

#pragma warning restore OPENAI001
