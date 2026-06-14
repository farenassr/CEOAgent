using CeoAgent.ApiService.Infrastructure.OpenApi;
using FastEndpoints;
using Microsoft.Extensions.Options;
using System.Text;

namespace CeoAgent.ApiService.Modules.WhatsApp;

public sealed partial class WhatsAppWebhookEndpoint(
    WhatsAppWebhookIngestionService ingestionService,
    IWhatsAppSignatureValidator signatureValidator,
    IOptions<WhatsAppOptions> whatsAppOptions,
    ILogger<WhatsAppWebhookEndpoint> logger) : EndpointWithoutRequest<WhatsAppWebhookIngestionResult>
{
    public override void Configure()
    {
        Post("/v1/whatsapp");
        AllowAnonymous();
        Description(builder => builder
            .WithTags(OpenApiConstants.Tags.Webhooks)
            .WithSummary("Receive WhatsApp Webhook")
            .WithDescription("Receives WhatsApp Cloud webhook callbacks, verifies the request signature, and ingests supported message events quickly. Use this as the public provider callback URL."));
        Summary(summary =>
        {
            summary.Summary = "Receive WhatsApp Webhook";
            summary.Description = "Receives WhatsApp Cloud webhook callbacks, verifies the request signature, and ingests supported message events quickly. Use this as the public provider callback URL.";
        });
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

        WhatsAppWebhookReceived(
            logger,
            request.Method,
            request.Path.Value,
            request.ContentType,
            request.ContentLength,
            !string.IsNullOrWhiteSpace(signature),
            signature.Length,
            bodyBytes.Length);



        if (!signatureValidator.IsValid(bodyBytes, signature, appSecret ?? string.Empty))
        {
            WhatsAppWebhookSignatureRejected(
                logger,
                request.Path.Value,
                !string.IsNullOrWhiteSpace(signature),
                signature.Length,
                bodyBytes.Length);

            await Send.ForbiddenAsync(cancellationToken);
            return;
        }

        var body = DecodeUtf8Body(bodyBytes);
        var correlationId = HttpContext.TraceIdentifier;
        try
        {
            var result = await ingestionService.IngestAsync(body, correlationId, cancellationToken);
            await Send.OkAsync(result, cancellationToken);
        }
        catch (InvalidWhatsAppWebhookPayloadException)
        {
            await Send.StatusCodeAsync(StatusCodes.Status400BadRequest, cancellationToken);
        }
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
