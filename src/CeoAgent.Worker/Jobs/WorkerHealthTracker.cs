namespace CeoAgent.Worker.Jobs;

public sealed class WorkerHealthTracker(TimeProvider timeProvider)
{
    private DateTimeOffset _lastPollTime = DateTimeOffset.MinValue;

    public void RecordPoll()
    {
        _lastPollTime = timeProvider.GetUtcNow();
    }

    public bool IsHealthy(TimeSpan threshold)
    {
        if (_lastPollTime == DateTimeOffset.MinValue)
        {
            return false;
        }

        return (timeProvider.GetUtcNow() - _lastPollTime) < threshold;
    }
}
