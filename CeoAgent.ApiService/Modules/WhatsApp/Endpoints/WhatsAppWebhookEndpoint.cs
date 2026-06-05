using FastEndpoints;
using Microsoft.Extensions.Options;

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
        Post("/v1/webhooks/whatsapp");
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

        using var reader = new StreamReader(HttpContext.Request.Body);
        var body = await reader.ReadToEndAsync(cancellationToken);
        if (System.Text.Encoding.UTF8.GetByteCount(body) > maxBodyBytes)
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
            body.Length);



        if (!signatureValidator.IsValid(body, signature, appSecret ?? string.Empty))
        {
            logger.LogWarning(
                WebhookSignatureRejectedEvent,
                "WhatsAppWebhookSignatureRejected Path={Path} SignaturePresent={SignaturePresent} SignatureLength={SignatureLength} BodyLength={BodyLength}",
                request.Path.Value,
                !string.IsNullOrWhiteSpace(signature),
                signature.Length,
                body.Length);

            await Send.ForbiddenAsync(cancellationToken);
            return;
        }

        var correlationId = HttpContext.TraceIdentifier;
        var result = await ingestionService.IngestAsync(body, correlationId, cancellationToken);
        await Send.OkAsync(result, cancellationToken);
    }

}
