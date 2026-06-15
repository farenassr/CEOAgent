namespace CeoAgent.Application.Abstractions.Payments;

public interface IPaymentQrImageProvider
{
    Task<PaymentQrImage> GetQrImageAsync(
        string blobContainer,
        string blobName,
        CancellationToken cancellationToken);
}
