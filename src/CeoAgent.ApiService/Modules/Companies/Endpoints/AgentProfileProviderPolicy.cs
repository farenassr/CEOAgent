using CeoAgent.Application.Errors;
using CeoAgent.Shared.Enums;
using CeoAgent.Shared.Request.Company;

namespace CeoAgent.ApiService.Modules.Companies.Endpoints;

internal static class AgentProfileProviderPolicy
{
    public const string LocalOllamaModelName = "hf.co/unsloth/gemma-4-E2B-it-GGUF:Q4_K_M";

    public static void Validate(AgentProfileRequest request, IHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(environment);

        if (request.LlmProvider != LlmProvider.Ollama)
        {
            return;
        }

        if (!IsLocalDevelopmentOrTesting(environment))
        {
            throw new BusinessRuleException(
                "ollama_local_only",
                "Ollama is only supported in Local, Development, or Testing environments.");
        }

        if (!string.Equals(request.ModelName, LocalOllamaModelName, StringComparison.Ordinal))
        {
            throw new BusinessRuleException(
                "ollama_model_unsupported",
                $"Ollama local profiles must use '{LocalOllamaModelName}'.");
        }
    }

    private static bool IsLocalDevelopmentOrTesting(IHostEnvironment environment)
    {
        return environment.IsDevelopment()
            || environment.IsEnvironment("Local")
            || environment.IsEnvironment("Testing");
    }
}
