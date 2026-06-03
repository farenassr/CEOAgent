using CeoAgent.Integrations.Speech;

namespace CeoAgent.Adapters.Speech;

internal sealed class UnavailableSpeechSynthesisIntegration : ISpeechSynthesisIntegration
{
    public Task<SpeechSynthesisResult> SynthesizeAsync(
        SpeechSynthesisRequest request,
        CancellationToken cancellationToken)
    {
        throw new NotSupportedException("Speech synthesis is not configured for this worker runtime.");
    }
}
