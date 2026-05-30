using FastEndpoints;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;

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
        Post("/webhooks/whatsapp");
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
            "WhatsAppWebhookReceived Method={Method} Path={Path} QueryString={QueryString} ContentType={ContentType} ContentLength={ContentLength} RemoteIpAddress={RemoteIpAddress} UserAgent={UserAgent} SignaturePresent={SignaturePresent} SignatureLength={SignatureLength} SignaturePrefix={SignaturePrefix} BodyLength={BodyLength}",
            request.Method,
            request.Path.Value,
            request.QueryString.Value,
            request.ContentType,
            request.ContentLength,
            HttpContext.Connection.RemoteIpAddress?.ToString(),
            HeaderValue(request.Headers.UserAgent),
            !string.IsNullOrWhiteSpace(signature),
            signature.Length,
            Prefix(signature),
            body.Length);

        if (logger.IsEnabled(LogLevel.Debug))
        {
            logger.LogDebug("WhatsAppWebhookReceived Body={Body}", body);
        }

        if (!signatureValidator.IsValid(body, signature, appSecret ?? string.Empty))
        {
            logger.LogWarning(
                WebhookSignatureRejectedEvent,
                "WhatsAppWebhookSignatureRejected Path={Path} SignaturePresent={SignaturePresent} SignatureLength={SignatureLength} SignaturePrefix={SignaturePrefix} BodyLength={BodyLength}",
                request.Path.Value,
                !string.IsNullOrWhiteSpace(signature),
                signature.Length,
                Prefix(signature),
                body.Length);

            await Send.ForbiddenAsync(cancellationToken);
            return;
        }

        var correlationId = HttpContext.TraceIdentifier;
        var result = await ingestionService.IngestAsync(body, correlationId, cancellationToken);
        await Send.OkAsync(result, cancellationToken);
    }

    private static string? HeaderValue(StringValues value)
    {
        return value.Count == 0 ? null : value.ToString();
    }

    private static string? Prefix(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.Length <= 16 ? value : value[..16];
    }
}
