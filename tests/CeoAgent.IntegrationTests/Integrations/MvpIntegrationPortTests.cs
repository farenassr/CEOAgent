using CeoAgent.Application.Abstractions.Messaging;
using CeoAgent.Shared.Messaging;
using Shouldly;

namespace CeoAgent.IntegrationTests.Integrations;

public sealed class MvpIntegrationPortTests
{
    [Test]
    public void MessagingPort_ExposesWhatsAppReadAndTextReplyOperations()
    {
        typeof(IMessageChannelIntegration).GetMethod(nameof(IMessageChannelIntegration.MarkMessageReadAsync)).ShouldNotBeNull();
        typeof(IMessageChannelIntegration).GetMethod(nameof(IMessageChannelIntegration.SendTextAsync)).ShouldNotBeNull();
    }
}
