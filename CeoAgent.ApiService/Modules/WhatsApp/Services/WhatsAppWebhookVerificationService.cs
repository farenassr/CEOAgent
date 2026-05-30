using Microsoft.Extensions.Options;

namespace CeoAgent.ApiService.Modules.WhatsApp;

/// <summary>
/// Verifies WhatsApp webhook subscription challenges against the configured verify token.
/// </summary>
public sealed class WhatsAppWebhookVerificationService(IOptions<WhatsAppOptions> whatsAppOptions)
{
    private const string SubscribeMode = "subscribe";

    /// <summary>
    /// Returns the challenge only when WhatsApp sends subscribe mode and the expected verification token.
    /// </summary>
    public string? Verify(string? mode, string? verifyToken, string? challenge)
    {
        if (!string.Equals(mode, SubscribeMode, StringComparison.Ordinal)
            || string.IsNullOrWhiteSpace(challenge))
        {
            return null;
        }

        var configuredVerifyToken = whatsAppOptions.Value.VerifyToken;
        return !string.IsNullOrWhiteSpace(configuredVerifyToken)
            && string.Equals(verifyToken, configuredVerifyToken, StringComparison.Ordinal)
                ? challenge
                : null;
    }
}
