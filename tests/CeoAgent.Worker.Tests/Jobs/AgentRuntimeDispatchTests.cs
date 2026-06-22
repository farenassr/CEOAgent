using CeoAgent.Infrastructure.Implementation.AI;
using CeoAgent.Shared.AI;
using CeoAgent.Shared.Enums;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Shouldly;

namespace CeoAgent.Worker.Tests.Jobs;

public sealed class AgentRuntimeDispatchTests
{
    [Test]
    public async Task RunTurnAsync_DispatchesToProviderMatchingRequestProvider()
    {
        var openAi = new FakeAgentRuntimeProvider(LlmProvider.OpenAI);
        var ollama = new FakeAgentRuntimeProvider(LlmProvider.Ollama, "local response");
        var gemini = new FakeAgentRuntimeProvider(LlmProvider.Gemini, "gemini response");
        var runtime = new ProviderDispatchingAgentRuntime(
            [openAi, ollama, gemini],
            new TestHostEnvironment("Testing"));

        var result = await runtime.RunTurnAsync(CreateRequest(LlmProvider.Gemini), CancellationToken.None);

        result.AssistantText.ShouldBe("gemini response");
        openAi.RunCount.ShouldBe(0);
        ollama.RunCount.ShouldBe(0);
        gemini.RunCount.ShouldBe(1);
    }

    [Test]
    public void CanEstimateCost_UsesProviderMatchingRequestedProvider()
    {
        var runtime = new ProviderDispatchingAgentRuntime(
            [
                new FakeAgentRuntimeProvider(LlmProvider.OpenAI, canEstimateCost: true),
                new FakeAgentRuntimeProvider(LlmProvider.Ollama, canEstimateCost: false),
                new FakeAgentRuntimeProvider(LlmProvider.Gemini, canEstimateCost: false),
            ],
            new TestHostEnvironment("Testing"));

        runtime.CanEstimateCost(LlmProvider.OpenAI, "gpt-4.1-mini").ShouldBeTrue();
        runtime.CanEstimateCost(LlmProvider.Ollama, "hf.co/unsloth/gemma-4-E2B-it-GGUF:Q4_K_M").ShouldBeFalse();
        runtime.CanEstimateCost(LlmProvider.Gemini, "gemini-3.5-flash").ShouldBeFalse();
    }

    [Test]
    public async Task RunTurnAsync_WhenGeminiRunsInProduction_DispatchesToProvider()
    {
        var gemini = new FakeAgentRuntimeProvider(LlmProvider.Gemini, "gemini response");
        var runtime = new ProviderDispatchingAgentRuntime(
            [gemini],
            new TestHostEnvironment("Production"));

        var result = await runtime.RunTurnAsync(CreateRequest(LlmProvider.Gemini), CancellationToken.None);

        result.AssistantText.ShouldBe("gemini response");
        gemini.RunCount.ShouldBe(1);
    }

    [Test]
    public async Task RunTurnAsync_WhenProviderMissing_ThrowsNotSupportedException()
    {
        var runtime = new ProviderDispatchingAgentRuntime(
            [new FakeAgentRuntimeProvider(LlmProvider.OpenAI)],
            new TestHostEnvironment("Testing"));

        var exception = await Should.ThrowAsync<NotSupportedException>(
            () => runtime.RunTurnAsync(CreateRequest(LlmProvider.Ollama), CancellationToken.None));

        exception.Message.ShouldContain("LLM provider 'Ollama' is not supported.");
    }

    [Test]
    public async Task RunTurnAsync_WhenOllamaRunsOutsideLocalDevelopmentOrTesting_ThrowsNotSupportedException()
    {
        var runtime = new ProviderDispatchingAgentRuntime(
            [new FakeAgentRuntimeProvider(LlmProvider.Ollama)],
            new TestHostEnvironment("Production"));

        var exception = await Should.ThrowAsync<NotSupportedException>(
            () => runtime.RunTurnAsync(CreateRequest(LlmProvider.Ollama), CancellationToken.None));

        exception.Message.ShouldContain("Ollama is only supported in Local, Development, or Testing environments.");
    }

    private static AgentTurnRequest CreateRequest(LlmProvider provider)
    {
        return new AgentTurnRequest(
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            provider,
            provider switch
            {
                LlmProvider.Ollama => "hf.co/unsloth/gemma-4-E2B-it-GGUF:Q4_K_M",
                LlmProvider.Gemini => "gemini-3.5-flash",
                _ => "gpt-4.1-mini",
            },
            "You are a helpful assistant.",
            "Hola");
    }

    private sealed class FakeAgentRuntimeProvider(
        LlmProvider provider,
        string assistantText = "provider response",
        bool canEstimateCost = false) : IAgentRuntimeProvider
    {
        public LlmProvider Provider { get; } = provider;

        public int RunCount { get; private set; }

        public bool CanEstimateCost(string modelName)
        {
            return canEstimateCost;
        }

        public Task<AgentTurnResult> RunTurnAsync(
            AgentTurnRequest request,
            CancellationToken cancellationToken)
        {
            RunCount++;
            return Task.FromResult(new AgentTurnResult(assistantText));
        }
    }

    private sealed class TestHostEnvironment(string environmentName) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = environmentName;

        public string ApplicationName { get; set; } = "CeoAgent.Worker.Tests";

        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
