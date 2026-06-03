namespace CeoAgent.Worker.Jobs;

internal sealed class UnavailableAudioBlobStore : IAudioBlobStore
{
    public Task<AudioBlobStoreResult> StoreAsync(
        AudioBlobStoreRequest request,
        CancellationToken cancellationToken)
    {
        throw new NotSupportedException("Audio blob storage is not configured for this worker runtime.");
    }
}
