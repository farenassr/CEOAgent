using CeoAgent.Application.Abstractions.Storage;
using CeoAgent.Infrastructure.Implementation.Messaging.Storage;
using CeoAgent.Shared.Storage;
using Shouldly;

namespace CeoAgent.IntegrationTests.Infrastructure;

public sealed class BlobStoredFileReaderTests
{
    [Test]
    public async Task ReadAsync_ReturnsDownloadedFileWithConfiguredFallbacks()
    {
        var reference = BlobStorageReference.Create(
            BlobStorageContainerNames.Private,
            "organizations/demo/payments/payment-accounts/demo/qr.png");
        var blobStorage = new FakeBlobStorageService(
            new BlobStorageDownloadResult([1, 2, 3], ContentType: string.Empty, FileName: string.Empty));
        var reader = new BlobStoredFileReader(blobStorage);

        var file = await reader.ReadAsync(
            reference,
            new StoredFileReadOptions(DefaultContentType: "image/png", DefaultFileName: "payment-qr.png"),
            CancellationToken.None);

        file.Content.ShouldBe([1, 2, 3]);
        file.ContentType.ShouldBe("image/png");
        file.FileName.ShouldBe("payment-qr.png");
    }

    private sealed class FakeBlobStorageService(BlobStorageDownloadResult result) : IBlobStorageService
    {
        public Task<BlobStorageUploadResult> UploadAsync(BlobStorageUploadRequest request, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task<BlobStorageDownloadResult> DownloadAsync(
            BlobStorageReference reference,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(result);
        }

        public Task<bool> ExistsAsync(BlobStorageReference reference, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task<bool> DeleteIfExistsAsync(BlobStorageReference reference, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task SetTagsAsync(
            BlobStorageReference reference,
            IReadOnlyDictionary<string, string> tags,
            CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task<IReadOnlyDictionary<string, string>> GetTagsAsync(
            BlobStorageReference reference,
            CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }
    }
}
