using CeoAgent.Shared.Messaging;

namespace CeoAgent.Application.Abstractions.Messaging;

public interface IOutboundMessageDispatcher
{
    Task<OutboundMessageDispatchResult> SendTextAsync(
        OutboundTextDispatchRequest request,
        CancellationToken cancellationToken);

    Task<OutboundMessageDispatchResult> SendImageAsync(
        OutboundImageDispatchRequest request,
        CancellationToken cancellationToken);
}
