using CeoAgent.Infrastructure.Implementation.AI;
using CeoAgent.Shared.AI;
using CeoAgent.Shared.Enums;
using Shouldly;
using System.Text.Json;

namespace CeoAgent.Worker.Tests.Jobs;

public sealed class OllamaAgentRuntimeOptionsTests
{
    [Test]
    public void CreateChatOptions_DisablesThinkingForLocalQwen()
    {
        var request = new AgentTurnRequest(
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            LlmProvider.Ollama,
            "hf.co/unsloth/gemma-4-E2B-it-GGUF:Q4_K_M",
            "You are a helpful assistant.",
            "Hola",
            MaxOutputTokenCount: 256);

        var options = OllamaAgentRuntime.CreateChatOptions(
            request,
            new AgentRuntimeOptions
            {
                AllowMultipleToolCalls = true,
            });

        options.ModelId.ShouldBe("hf.co/unsloth/gemma-4-E2B-it-GGUF:Q4_K_M");
        options.MaxOutputTokens.ShouldBe(256);
        options.AllowMultipleToolCalls.ShouldBe(true);
        options.AdditionalProperties.ShouldNotBeNull();
        options.AdditionalProperties["think"].ShouldBe(false);
    }

    [Test]
    public void NormalizeToolSchema_ForOllama_ReplacesNullableTypeArraysAndRemovesNullableRequiredFields()
    {
        var schema = JsonDocument.Parse(
            """
            {
              "type": "object",
              "properties": {
                "reservationId": { "type": "string" },
                "reason": { "type": ["string", "null"] }
              },
              "required": ["reservationId", "reason"],
              "additionalProperties": false
            }
            """).RootElement.Clone();

        var normalized = OllamaToolJsonSchema.Normalize(schema);

        normalized.GetProperty("properties")
            .GetProperty("reason")
            .GetProperty("type")
            .GetString()
            .ShouldBe("string");
        normalized.GetProperty("required")
            .EnumerateArray()
            .Select(element => element.GetString())
            .ShouldBe(["reservationId"]);
    }
}
