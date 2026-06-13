using CeoAgent.ApiService.Infrastructure.OpenApi;
using FastEndpoints;

namespace CeoAgent.ApiService.Modules.WhatsApp;

public sealed class WhatsAppWebhookVerificationEndpoint(
    WhatsAppWebhookVerificationService verificationService,
    ILogger<WhatsAppWebhookVerificationEndpoint> logger) : EndpointWithoutRequest
{
    private static readonly EventId VerificationRequestedEvent = new(2201, "WhatsAppWebhookVerificationRequested");
    private static readonly EventId VerificationRejectedEvent = new(2202, "WhatsAppWebhookVerificationRejected");

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

        logger.LogInformation(
            VerificationRequestedEvent,
            "WhatsAppWebhookVerificationRequested Mode={Mode} VerifyTokenPresent={VerifyTokenPresent} VerifyTokenLength={VerifyTokenLength} ChallengePresent={ChallengePresent} ChallengeLength={ChallengeLength}",
            mode,
            !string.IsNullOrWhiteSpace(verifyToken),
            verifyToken?.Length,
            !string.IsNullOrWhiteSpace(challenge),
            challenge?.Length);

        if (verifiedChallenge is null)
        {
            logger.LogWarning(
                VerificationRejectedEvent,
                "WhatsAppWebhookVerificationRejected Mode={Mode} VerifyTokenPresent={VerifyTokenPresent} ChallengePresent={ChallengePresent}",
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
