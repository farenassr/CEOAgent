using Microsoft.Extensions.Logging;

namespace CeoAgent.ApiService.Modules.WhatsApp;

public sealed partial class WhatsAppWebhookEndpoint
{
    [LoggerMessage(
        EventId = 4201,
        Level = LogLevel.Information,
        Message = "WhatsAppWebhookReceived Method={Method} Path={Path} ContentType={ContentType} ContentLength={ContentLength} SignaturePresent={SignaturePresent} SignatureLength={SignatureLength} BodyLength={BodyLength}")]
    private static partial void WhatsAppWebhookReceived(
        ILogger logger,
        string method,
        string? path,
        string? contentType,
        long? contentLength,
        bool signaturePresent,
        int signatureLength,
        int bodyLength);

    [LoggerMessage(
        EventId = 4204,
        Level = LogLevel.Warning,
        Message = "WhatsAppWebhookSignatureRejected Path={Path} SignaturePresent={SignaturePresent} SignatureLength={SignatureLength} BodyLength={BodyLength}")]
    private static partial void WhatsAppWebhookSignatureRejected(
        ILogger logger,
        string? path,
        bool signaturePresent,
        int signatureLength,
        int bodyLength);
}
