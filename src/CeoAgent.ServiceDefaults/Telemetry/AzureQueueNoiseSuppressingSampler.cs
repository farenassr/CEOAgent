using System.Collections.Concurrent;
using OpenTelemetry.Trace;

namespace CeoAgent.ServiceDefaults.Telemetry;

public sealed class AzureQueueNoiseSuppressingSampler(
    Sampler innerSampler,
    TimeSpan sampleInterval,
    TimeProvider? timeProvider = null) : Sampler
{
    private static readonly HashSet<string> SuppressedOperationNames = new(StringComparer.Ordinal)
    {
        "QueueClient.ReceiveMessages",
        "QueueClient.GetProperties",
        "QueueServiceClient.GetProperties",
    };

    private readonly ConcurrentDictionary<string, long> lastSampleTicksByOperation = new(StringComparer.Ordinal);
    private readonly TimeProvider timeProvider = timeProvider ?? TimeProvider.System;

    public override SamplingResult ShouldSample(in SamplingParameters samplingParameters)
    {
        if (!SuppressedOperationNames.Contains(samplingParameters.Name))
        {
            return innerSampler.ShouldSample(samplingParameters);
        }

        var nowTicks = timeProvider.GetUtcNow().UtcTicks;
        var shouldSample = false;

        lastSampleTicksByOperation.AddOrUpdate(
            samplingParameters.Name,
            _ =>
            {
                shouldSample = true;
                return nowTicks;
            },
            (_, lastSampleTicks) =>
            {
                if (nowTicks - lastSampleTicks < sampleInterval.Ticks)
                {
                    return lastSampleTicks;
                }

                shouldSample = true;
                return nowTicks;
            });

        return shouldSample
            ? innerSampler.ShouldSample(samplingParameters)
            : new SamplingResult(SamplingDecision.Drop);
    }

}
