namespace CeoAgent.Infrastructure.Implementation.OpenAI;

public sealed class OpenAIAgentRuntimeOptions
{
    public const string SectionName = "LlmProviders:OpenAI";

    public string ApiKeyReference { get; set; } = string.Empty;
}
