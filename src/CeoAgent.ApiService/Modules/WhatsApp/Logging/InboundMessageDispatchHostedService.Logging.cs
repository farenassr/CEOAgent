using Microsoft.Extensions.Logging;

namespace CeoAgent.ApiService.Modules.WhatsApp;

public sealed partial class InboundMessageDispatchHostedService
{
    [LoggerMessage(
        EventId = 2103,
        Level = LogLevel.Warning,
        Message = "InboundMessageDispatchHostedDispatchFailed")]
    private static partial void InboundMessageDispatchHostedDispatchFailed(
        ILogger logger,
        Exception exception);
}
