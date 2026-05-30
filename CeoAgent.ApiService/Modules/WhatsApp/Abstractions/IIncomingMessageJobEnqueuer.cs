using CeoAgent.Integrations.Jobs;

namespace CeoAgent.ApiService.Modules.WhatsApp;

public interface IIncomingMessageJobEnqueuer
{
    Task EnqueueAsync(ProcessIncomingMessageJob job, CancellationToken cancellationToken);
}
