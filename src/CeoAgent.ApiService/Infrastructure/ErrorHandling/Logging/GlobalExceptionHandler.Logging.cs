using Microsoft.Extensions.Logging;

namespace CeoAgent.ApiService.Infrastructure.ErrorHandling;

public sealed partial class GlobalExceptionHandler
{
    [LoggerMessage(
        EventId = 1001,
        Level = LogLevel.Information,
        Message = "RequestCancelledByClient CorrelationId={CorrelationId}")]
    private static partial void RequestCancelledByClient(ILogger logger, string? correlationId);

    [LoggerMessage(
        EventId = 1002,
        Level = LogLevel.Error,
        Message = "RequestFailed Status={Status} CorrelationId={CorrelationId}")]
    private static partial void RequestFailed(
        ILogger logger,
        Exception exception,
        int status,
        string? correlationId);
}
