using FastEndpoints;
using Microsoft.Extensions.Options;
using System.Text;

namespace CeoAgent.ApiService.Modules.WhatsApp;

public sealed class WhatsAppWebhookEndpoint(
    WhatsAppWebhookIngestionService ingestionService,
    IWhatsAppSignatureValidator signatureValidator,
    IOptions<WhatsAppOptions> whatsAppOptions,
    ILogger<WhatsAppWebhookEndpoint> logger) : EndpointWithoutRequest<WhatsAppWebhookIngestionResult>
{
    private static readonly EventId WebhookReceivedEvent = new(2001, "WhatsAppWebhookReceived");
    private static readonly EventId WebhookSignatureRejectedEvent = new(2002, "WhatsAppWebhookSignatureRejected");

    public override void Configure()
    {
        Post("/v1/whatsapp");
        AllowAnonymous();
    }

    public override async Task HandleAsync(CancellationToken cancellationToken)
    {
        var maxBodyBytes = whatsAppOptions.Value.MaxWebhookBodyBytes;
        if (HttpContext.Request.ContentLength > maxBodyBytes)
        {
            await Send.StatusCodeAsync(StatusCodes.Status413PayloadTooLarge, cancellationToken);
            return;
        }

        var bodyBytes = await ReadBodyBytesAsync(HttpContext.Request.Body, maxBodyBytes, cancellationToken);
        if (bodyBytes is null)
        {
            await Send.StatusCodeAsync(StatusCodes.Status413PayloadTooLarge, cancellationToken);
            return;
        }

        var signature = HttpContext.Request.Headers["X-Hub-Signature-256"].ToString();
        var appSecret = whatsAppOptions.Value.AppSecret;
        var request = HttpContext.Request;

        logger.LogInformation(
            WebhookReceivedEvent,
            "WhatsAppWebhookReceived Method={Method} Path={Path} ContentType={ContentType} ContentLength={ContentLength} SignaturePresent={SignaturePresent} SignatureLength={SignatureLength} BodyLength={BodyLength}",
            request.Method,
            request.Path.Value,
            request.ContentType,
            request.ContentLength,
            !string.IsNullOrWhiteSpace(signature),
            signature.Length,
            bodyBytes.Length);



        if (!signatureValidator.IsValid(bodyBytes, signature, appSecret ?? string.Empty))
        {
            logger.LogWarning(
                WebhookSignatureRejectedEvent,
                "WhatsAppWebhookSignatureRejected Path={Path} SignaturePresent={SignaturePresent} SignatureLength={SignatureLength} BodyLength={BodyLength}",
                request.Path.Value,
                !string.IsNullOrWhiteSpace(signature),
                signature.Length,
                bodyBytes.Length);

            await Send.ForbiddenAsync(cancellationToken);
            return;
        }

        var body = DecodeUtf8Body(bodyBytes);
        var correlationId = HttpContext.TraceIdentifier;
        var result = await ingestionService.IngestAsync(body, correlationId, cancellationToken);
        await Send.OkAsync(result, cancellationToken);
    }

    private static async Task<byte[]?> ReadBodyBytesAsync(
        Stream body,
        int maxBodyBytes,
        CancellationToken cancellationToken)
    {
        using var buffer = new MemoryStream(capacity: Math.Min(maxBodyBytes, 64 * 1024));
        var chunk = new byte[16 * 1024];

        while (true)
        {
            var bytesRead = await body.ReadAsync(chunk, cancellationToken);
            if (bytesRead == 0)
            {
                return buffer.ToArray();
            }

            if (buffer.Length + bytesRead > maxBodyBytes)
            {
                return null;
            }

            buffer.Write(chunk, 0, bytesRead);
        }
    }

    private static string DecodeUtf8Body(byte[] bodyBytes)
    {
        var body = bodyBytes.AsSpan();
        var preamble = Encoding.UTF8.GetPreamble();
        if (body.StartsWith(preamble))
        {
            body = body[preamble.Length..];
        }

        return Encoding.UTF8.GetString(body);
    }

}
