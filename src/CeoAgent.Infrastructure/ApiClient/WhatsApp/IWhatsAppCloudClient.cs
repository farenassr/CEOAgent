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

    [Multipart]
    [Post("/{phoneNumberId}/media")]
    Task<WhatsAppUploadMediaResponse> UploadMediaAsync(
        string phoneNumberId,
        [Header("Authorization")] string authorization,
        [AliasAs("messaging_product")] string messagingProduct,
        [AliasAs("file")] StreamPart file,
        CancellationToken cancellationToken);
}
