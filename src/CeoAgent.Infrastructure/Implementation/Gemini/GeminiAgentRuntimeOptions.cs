namespace CeoAgent.Infrastructure.Implementation.Gemini;

public sealed class GeminiAgentRuntimeOptions
{
    public const string SectionName = "LlmProviders:Gemini";

    public string ApiKeyReference { get; set; } = string.Empty;
}
