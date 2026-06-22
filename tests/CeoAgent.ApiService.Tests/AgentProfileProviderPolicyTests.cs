using System.Text.Json;
using CeoAgent.ApiService.Modules.Companies.Endpoints;
using CeoAgent.Application.Errors;
using CeoAgent.Shared.Enums;
using CeoAgent.Shared.Request.Company;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Shouldly;

namespace CeoAgent.ApiService.Tests;

public sealed class AgentProfileProviderPolicyTests
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    [Test]
    public void AgentProfileRequest_AcceptsOllamaJsonProvider()
    {
        var request = JsonSerializer.Deserialize<AgentProfileRequest>(
            """
            {
              "modelName": "hf.co/unsloth/gemma-4-E2B-it-GGUF:Q4_K_M",
              "llmProvider": "ollama",
              "displayName": "Contoso Assistant",
              "language": "es",
              "timeZoneId": "America/Bogota",
              "maxEstimatedCostUsdPerJob": 0
            }
            """,
            SerializerOptions);

        request.ShouldNotBeNull();
        request.LlmProvider.ShouldBe(LlmProvider.Ollama);
        request.ModelName.ShouldBe("hf.co/unsloth/gemma-4-E2B-it-GGUF:Q4_K_M");
    }

    [Test]
    public void AgentProfileRequest_AcceptsGeminiJsonProvider()
    {
        var request = JsonSerializer.Deserialize<AgentProfileRequest>(
            """
            {
              "modelName": "gemini-3.5-flash",
              "llmProvider": "gemini",
              "displayName": "Contoso Assistant",
              "language": "es",
              "timeZoneId": "America/Bogota",
              "maxEstimatedCostUsdPerJob": 0
            }
            """,
            SerializerOptions);

        request.ShouldNotBeNull();
        request.LlmProvider.ShouldBe(LlmProvider.Gemini);
        request.ModelName.ShouldBe("gemini-3.5-flash");
    }

    [Test]
    public void Validate_WhenTestingEnvironment_AllowsLocalOllamaModel()
    {
        var request = CreateOllamaRequest("hf.co/unsloth/gemma-4-E2B-it-GGUF:Q4_K_M");

        Should.NotThrow(() => AgentProfileProviderPolicy.Validate(
            request,
            new TestHostEnvironment("Testing")));
    }

    [Test]
    public void Validate_WhenProductionEnvironment_RejectsOllama()
    {
        var request = CreateOllamaRequest("hf.co/unsloth/gemma-4-E2B-it-GGUF:Q4_K_M");

        var exception = Should.Throw<BusinessRuleException>(() => AgentProfileProviderPolicy.Validate(
            request,
            new TestHostEnvironment("Production")));

        exception.Code.ShouldBe("ollama_local_only");
    }

    [Test]
    public void Validate_WhenOllamaModelIsNotPinnedLocalModel_RejectsRequest()
    {
        var request = CreateOllamaRequest("unsupported-local-model");

        var exception = Should.Throw<BusinessRuleException>(() => AgentProfileProviderPolicy.Validate(
            request,
            new TestHostEnvironment("Testing")));

        exception.Code.ShouldBe("ollama_model_unsupported");
    }

    [Test]
    public void Validate_WhenGeminiStableModelRequested_AllowsProfile()
    {
        var request = CreateGeminiRequest("gemini-3.5-flash");

        Should.NotThrow(() => AgentProfileProviderPolicy.Validate(
            request,
            new TestHostEnvironment("Production")));
    }

    [Test]
    public void Validate_WhenFutureGeminiModelRequested_AllowsProfile()
    {
        var request = CreateGeminiRequest("gemini-4.0-flash");

        Should.NotThrow(() => AgentProfileProviderPolicy.Validate(
            request,
            new TestHostEnvironment("Production")));
    }

    [Test]
    public void Validate_WhenGeminiProviderUsesNonGeminiModel_RejectsRequest()
    {
        var request = CreateGeminiRequest("gpt-5.4-nano");

        var exception = Should.Throw<BusinessRuleException>(() => AgentProfileProviderPolicy.Validate(
            request,
            new TestHostEnvironment("Production")));

        exception.Code.ShouldBe("gemini_model_unsupported");
    }

    private static AgentProfileRequest CreateOllamaRequest(string modelName)
    {
        return new AgentProfileRequest
        {
            ModelName = modelName,
            LlmProvider = LlmProvider.Ollama,
            DisplayName = "Contoso Assistant",
            Language = "es",
            TimeZoneId = "America/Bogota",
            MaxEstimatedCostUsdPerJob = 0,
        };
    }

    private static AgentProfileRequest CreateGeminiRequest(string modelName)
    {
        return new AgentProfileRequest
        {
            ModelName = modelName,
            LlmProvider = LlmProvider.Gemini,
            DisplayName = "Contoso Assistant",
            Language = "es",
            TimeZoneId = "America/Bogota",
            MaxEstimatedCostUsdPerJob = 0,
        };
    }

    private sealed class TestHostEnvironment(string environmentName) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = environmentName;

        public string ApplicationName { get; set; } = "CeoAgent.ApiService.Tests";

        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
