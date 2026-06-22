using CeoAgent.Infrastructure.Implementation.AI;
using CeoAgent.Shared.AI;
using CeoAgent.Shared.Enums;
using Shouldly;

namespace CeoAgent.Worker.Tests.Jobs;

public sealed class GeminiAgentRuntimeOptionsTests
{
    [Test]
    public void CreateChatOptions_UsesRequestedGeminiModelAndTokenLimit()
    {
        var request = new AgentTurnRequest(
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            LlmProvider.Gemini,
            "gemini-3.5-flash",
            "You are a helpful assistant.",
            "Hola",
            MaxOutputTokenCount: 512);

        var options = GeminiAgentRuntime.CreateChatOptions(
            request,
            new AgentRuntimeOptions
            {
                AllowMultipleToolCalls = true,
            });

        options.ModelId.ShouldBe("gemini-3.5-flash");
        options.MaxOutputTokens.ShouldBe(512);
        options.AllowMultipleToolCalls.ShouldBe(true);
    }
}
