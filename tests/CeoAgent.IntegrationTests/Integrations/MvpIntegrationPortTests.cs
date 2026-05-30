using CeoAgent.Integrations.Messaging;
using CeoAgent.Integrations.Speech;
using Shouldly;

namespace CeoAgent.IntegrationTests.Integrations;

public sealed class MvpIntegrationPortTests
{
    [Test]
    public void MessagingPort_ExposesWhatsAppReadMediaAndReplyOperations()
    {
        typeof(IMessageChannelIntegration).GetMethod(nameof(IMessageChannelIntegration.MarkMessageReadAsync)).ShouldNotBeNull();
        typeof(IMessageChannelIntegration).GetMethod(nameof(IMessageChannelIntegration.DownloadMediaAsync)).ShouldNotBeNull();
        typeof(IMessageChannelIntegration).GetMethod(nameof(IMessageChannelIntegration.SendTextAsync)).ShouldNotBeNull();
        typeof(IMessageChannelIntegration).GetMethod(nameof(IMessageChannelIntegration.SendAudioAsync)).ShouldNotBeNull();
    }

    [Test]
    public void SpeechPorts_KeepSttAndTtsSwappable()
    {
        typeof(ITranscriptionIntegration).GetMethod(nameof(ITranscriptionIntegration.TranscribeAsync)).ShouldNotBeNull();
        typeof(ISpeechSynthesisIntegration).GetMethod(nameof(ISpeechSynthesisIntegration.SynthesizeAsync)).ShouldNotBeNull();
    }
}
