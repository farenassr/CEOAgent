using CeoAgent.Integrations.Speech;

namespace CeoAgent.Adapters.Speech;

internal sealed class UnavailableTranscriptionIntegration : ITranscriptionIntegration
{
    public Task<TranscriptionResult> TranscribeAsync(
        TranscriptionRequest request,
        CancellationToken cancellationToken)
    {
        throw new NotSupportedException("Speech transcription is not configured for this worker runtime.");
    }
}
