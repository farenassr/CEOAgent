namespace CeoAgent.Worker.Jobs;

public interface IAudioBlobStore
{
    Task<AudioBlobStoreResult> StoreAsync(AudioBlobStoreRequest request, CancellationToken cancellationToken);
}
