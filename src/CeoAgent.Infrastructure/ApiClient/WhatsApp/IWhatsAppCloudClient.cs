using Refit;

namespace CeoAgent.Infrastructure.ApiClient.WhatsApp;

public interface IWhatsAppCloudClient
{
    [Post("/{phoneNumberId}/messages")]
    Task<WhatsAppSendMessageResponse> SendMessageAsync(
        string phoneNumberId,
        [Header("Authorization")] string authorization,
        [Body] WhatsAppSendMessageRequest request,
        CancellationToken cancellationToken);

}
