namespace CeoAgent.Integrations.Speech;

public interface ISpeechSynthesisIntegration
{
    Task<SpeechSynthesisResult> SynthesizeAsync(
        SpeechSynthesisRequest request,
        CancellationToken cancellationToken);
}
