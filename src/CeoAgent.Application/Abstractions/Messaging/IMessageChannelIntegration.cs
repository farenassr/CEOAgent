using CeoAgent.Shared.Messaging;

namespace CeoAgent.Application.Abstractions.Messaging;

public interface IMessageChannelIntegration
{
    Task MarkMessageReadAsync(
        ChannelMessageReference message,
        CancellationToken cancellationToken);

    Task<SentMessageReference> SendTextAsync(
        ChannelTextMessage message,
        CancellationToken cancellationToken);
}
