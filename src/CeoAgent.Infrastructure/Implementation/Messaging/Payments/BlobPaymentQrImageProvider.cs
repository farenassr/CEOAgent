using CeoAgent.Application.Abstractions.Payments;
using CeoAgent.Application.Abstractions.Storage;
using CeoAgent.Shared.Storage;

namespace CeoAgent.Infrastructure.Implementation.Messaging.Payments;

public sealed class BlobPaymentQrImageProvider(IStoredFileReader storedFileReader) : IPaymentQrImageProvider
{
    private static readonly StoredFileReadOptions ReadOptions = new(
        DefaultContentType: "image/png",
        DefaultFileName: "payment-qr.png");

    public async Task<PaymentQrImage> GetQrImageAsync(
        string blobContainer,
        string blobName,
        CancellationToken cancellationToken)
    {
        var file = await storedFileReader.ReadAsync(
            BlobStorageReference.Create(blobContainer, blobName),
            ReadOptions,
            cancellationToken);

        return new PaymentQrImage(file.Content, file.ContentType, file.FileName);
    }
}
