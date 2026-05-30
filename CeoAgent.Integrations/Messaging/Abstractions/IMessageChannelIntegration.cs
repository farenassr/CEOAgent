namespace CeoAgent.Integrations.Messaging;

public interface IMessageChannelIntegration
{
    Task MarkMessageReadAsync(
        ChannelMessageReference message,
        CancellationToken cancellationToken);

    Task<DownloadedMedia> DownloadMediaAsync(
        ChannelMediaReference media,
        CancellationToken cancellationToken);

    Task<SentMessageReference> SendTextAsync(
        ChannelTextMessage message,
        CancellationToken cancellationToken);

    Task<SentMessageReference> SendAudioAsync(
        ChannelAudioMessage message,
        CancellationToken cancellationToken);
}
