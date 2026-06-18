namespace CeoAgent.Infrastructure.Implementation.OpenAI;

public sealed class OpenAIAgentRuntimeOptions
{
    public const string SectionName = "LlmProviders:OpenAI";

    public string ApiKeyReference { get; set; } = string.Empty;
    public double InputTokenCostPerMillion { get; set; }
    public double OutputTokenCostPerMillion { get; set; }
    public Dictionary<string, OpenAIModelPricingOptions> Models { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public bool TryGetPricing(string modelName, out OpenAIModelPricingOptions pricing)
    {
        if (Models.TryGetValue(modelName, out pricing!) && pricing.IsConfigured)
        {
            return true;
        }

        pricing = new OpenAIModelPricingOptions
        {
            InputTokenCostPerMillion = InputTokenCostPerMillion,
            OutputTokenCostPerMillion = OutputTokenCostPerMillion,
        };
        return pricing.IsConfigured;
    }
}

public sealed class OpenAIModelPricingOptions
{
    public double InputTokenCostPerMillion { get; set; }
    public double OutputTokenCostPerMillion { get; set; }

    public bool IsConfigured => InputTokenCostPerMillion > 0 || OutputTokenCostPerMillion > 0;
}
