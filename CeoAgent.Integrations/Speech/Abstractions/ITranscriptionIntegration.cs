namespace CeoAgent.Integrations.Speech;

public interface ITranscriptionIntegration
{
    Task<TranscriptionResult> TranscribeAsync(
        TranscriptionRequest request,
        CancellationToken cancellationToken);
}
