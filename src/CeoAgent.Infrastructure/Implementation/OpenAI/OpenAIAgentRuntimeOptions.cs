namespace CeoAgent.Infrastructure.Implementation.OpenAI;

public sealed class OpenAIAgentRuntimeOptions
{
    public const string SectionName = "LlmProviders:OpenAI";

    public string ApiKeyReference { get; set; } = string.Empty;
    public double InputTokenCostPerMillion { get; set; }
    public double OutputTokenCostPerMillion { get; set; }
}
