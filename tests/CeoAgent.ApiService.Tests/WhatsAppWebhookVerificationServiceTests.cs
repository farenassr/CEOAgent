using CeoAgent.ApiService.Modules.WhatsApp;
using Microsoft.Extensions.Options;
using Shouldly;

namespace CeoAgent.ApiService.Tests;

public sealed class WhatsAppWebhookVerificationServiceTests
{
    [Test]
    public void Verify_WhenTokenMatches_ReturnsChallenge()
    {
        var service = new WhatsAppWebhookVerificationService(Options.Create(new WhatsAppOptions
        {
            VerifyToken = "local-verify-token",
        }));

        var challenge = service.Verify(
            mode: "subscribe",
            verifyToken: "local-verify-token",
            challenge: "challenge-123");

        challenge.ShouldBe("challenge-123");
    }

    [Test]
    public void Verify_WhenTokenDoesNotMatch_ReturnsNull()
    {
        var service = new WhatsAppWebhookVerificationService(Options.Create(new WhatsAppOptions
        {
            VerifyToken = "local-verify-token",
        }));

        var challenge = service.Verify(
            mode: "subscribe",
            verifyToken: "wrong-token",
            challenge: "challenge-123");

        challenge.ShouldBeNull();
    }
}
