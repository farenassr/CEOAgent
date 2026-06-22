using Microsoft.Extensions.AI;

namespace CeoAgent.Infrastructure.Implementation.Gemini;

internal interface IGeminiChatClientFactory
{
    Task<IChatClient> GetClientAsync(string modelName, CancellationToken cancellationToken);
}
