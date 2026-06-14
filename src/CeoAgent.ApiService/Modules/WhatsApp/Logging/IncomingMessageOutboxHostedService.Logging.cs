using Microsoft.Extensions.Logging;

namespace CeoAgent.ApiService.Modules.WhatsApp;

public sealed partial class IncomingMessageOutboxHostedService
{
    [LoggerMessage(
        EventId = 2103,
        Level = LogLevel.Warning,
        Message = "IncomingMessageOutboxHostedDispatchFailed")]
    private static partial void IncomingMessageOutboxHostedDispatchFailed(
        ILogger logger,
        Exception exception);
}
