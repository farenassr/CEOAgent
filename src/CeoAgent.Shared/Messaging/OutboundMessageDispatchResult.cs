namespace CeoAgent.Shared.Messaging;

public sealed record OutboundMessageDispatchResult(
    string ProviderMessageId,
    bool WasAlreadySent);
