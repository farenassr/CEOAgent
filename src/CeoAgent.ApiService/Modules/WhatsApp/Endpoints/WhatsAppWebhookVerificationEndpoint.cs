using CeoAgent.ApiService.Infrastructure.OpenApi;
using FastEndpoints;

namespace CeoAgent.ApiService.Modules.WhatsApp;

public sealed partial class WhatsAppWebhookVerificationEndpoint(
    WhatsAppWebhookVerificationService verificationService,
    ILogger<WhatsAppWebhookVerificationEndpoint> logger) : EndpointWithoutRequest
{
    public override void Configure()
    {
        Get("/v1/whatsapp/webhook");
        AllowAnonymous();
        Description(builder => builder
            .WithTags(OpenApiConstants.Tags.Webhooks)
            .WithSummary("Verify WhatsApp Webhook")
            .WithDescription("Handles WhatsApp Cloud webhook verification challenges using the configured verify token. Use this endpoint when registering the provider callback URL."));
        Summary(summary =>
        {
            summary.Summary = "Verify WhatsApp Webhook";
            summary.Description = "Handles WhatsApp Cloud webhook verification challenges using the configured verify token. Use this endpoint when registering the provider callback URL.";
        });
    }

    public override async Task HandleAsync(CancellationToken cancellationToken)
    {
        var mode = Query<string?>("hub.mode", isRequired: false);
        var verifyToken = Query<string?>("hub.verify_token", isRequired: false);
        var challenge = Query<string?>("hub.challenge", isRequired: false);
        var verifiedChallenge = verificationService.Verify(mode, verifyToken, challenge);

        WhatsAppWebhookVerificationRequested(
            logger,
            mode,
            !string.IsNullOrWhiteSpace(verifyToken),
            verifyToken?.Length,
            !string.IsNullOrWhiteSpace(challenge),
            challenge?.Length);

        if (verifiedChallenge is null)
        {
            WhatsAppWebhookVerificationRejected(
                logger,
                mode,
                !string.IsNullOrWhiteSpace(verifyToken),
                !string.IsNullOrWhiteSpace(challenge));

            await Send.ForbiddenAsync(cancellationToken);
            return;
        }

        HttpContext.Response.ContentType = "text/plain";
        await HttpContext.Response.WriteAsync(verifiedChallenge, cancellationToken);
    }

}
