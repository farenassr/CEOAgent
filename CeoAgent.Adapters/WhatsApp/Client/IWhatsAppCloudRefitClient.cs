using Refit;
using CeoAgent.Adapters.WhatsApp.Abstractions;

namespace CeoAgent.Adapters.WhatsApp.Client;

public interface IWhatsAppCloudRefitClient
{
    [Post("/{phoneNumberId}/messages")]
    Task<WhatsAppSendMessageResponse> SendMessageAsync(
        string phoneNumberId,
        [Header("Authorization")] string authorization,
        [Body] WhatsAppSendMessageRequest request,
        CancellationToken cancellationToken);

    [Get("/{mediaId}")]
    Task<WhatsAppMediaResponse> GetMediaAsync(
        string mediaId,
        [Header("Authorization")] string authorization,
        CancellationToken cancellationToken);
}
