using CeoAgent.Application.Abstractions.Jobs;
using CeoAgent.Shared.Jobs;

namespace CeoAgent.ApiService.Modules.WhatsApp;

public interface IIncomingMessageJobEnqueuer
{
    Task EnqueueAsync(ProcessIncomingMessageJob job, CancellationToken cancellationToken);
}
