using Microsoft.Extensions.Logging;

namespace CeoAgent.ApiService.Modules.WhatsApp;

public sealed partial class WhatsAppWebhookVerificationEndpoint
{
    [LoggerMessage(
        EventId = 4211,
        Level = LogLevel.Information,
        Message = "WhatsAppWebhookVerificationRequested Mode={Mode} VerifyTokenPresent={VerifyTokenPresent} VerifyTokenLength={VerifyTokenLength} ChallengePresent={ChallengePresent} ChallengeLength={ChallengeLength}")]
    private static partial void WhatsAppWebhookVerificationRequested(
        ILogger logger,
        string? mode,
        bool verifyTokenPresent,
        int? verifyTokenLength,
        bool challengePresent,
        int? challengeLength);

    [LoggerMessage(
        EventId = 4212,
        Level = LogLevel.Warning,
        Message = "WhatsAppWebhookVerificationRejected Mode={Mode} VerifyTokenPresent={VerifyTokenPresent} ChallengePresent={ChallengePresent}")]
    private static partial void WhatsAppWebhookVerificationRejected(
        ILogger logger,
        string? mode,
        bool verifyTokenPresent,
        bool challengePresent);
}
