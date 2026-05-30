using CeoAgent.Integrations.Jobs;

namespace CeoAgent.ApiService.Modules.WhatsApp;

public sealed class UnavailableIncomingMessageJobEnqueuer : IIncomingMessageJobEnqueuer
{
    public Task EnqueueAsync(ProcessIncomingMessageJob job, CancellationToken cancellationToken)
    {
        throw new InvalidOperationException("Incoming message queue is not configured.");
    }
}
