using CeoAgent.ApiService.Infrastructure.Storage;
using CeoAgent.Infrastructure.Entities;
using CeoAgent.Shared.Storage;

namespace CeoAgent.ApiService.Modules.Payments;

internal static class PaymentAccountQrFilePolicy
{
    public const string FormFieldName = "qrImage";

    public static FileUploadValidationOptions ValidationOptions { get; } = new(
        RequiredMessage: "QR image is required.",
        InvalidContentTypeMessage: "QR image must be a PNG or JPEG file.",
        AllowedContentTypes: ["image/png", "image/jpeg"]);

    public static BlobStorageReference ReferenceFor(CompanyPaymentAccount account)
    {
        return BlobStorageReference.Create(account.QrBlobContainer, account.QrBlobName);
    }

    public static void ApplyReference(CompanyPaymentAccount account, string fileName)
    {
        ArgumentNullException.ThrowIfNull(account);

        var reference = BlobStorageNaming.ForPaymentQr(fileName, account.Id);
        account.QrBlobContainer = reference.ContainerName;
        account.QrBlobName = reference.BlobName;
        account.QrBlobUri = null;
    }

    public static IReadOnlyDictionary<string, string> TagsFor(CompanyPaymentAccount account)
    {
        return BlobStorageTags.ForPaymentQr(account.OrganizationId, account.Id);
    }
}
