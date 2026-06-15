using CeoAgent.ApiService.Infrastructure.Storage;
using CeoAgent.Application.Abstractions.Storage;
using CeoAgent.Shared.Storage;
using Microsoft.AspNetCore.Http;
using Shouldly;

namespace CeoAgent.ApiService.Tests;

public sealed class FormFileBlobUploadTests
{
    [Test]
    public async Task UploadAsync_StoresAnyAllowedFormFileWithNormalizedContentTypeAndTags()
    {
        var blobStorage = new FakeBlobStorageService();
        var reference = BlobStorageReference.Create(
            BlobStorageContainerNames.Private,
            "organizations/demo/files/menu.jpg");
        var tags = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["organization_id"] = Guid.CreateVersion7().ToString("D"),
            ["visibility"] = "private",
            ["category"] = "payment_qr",
            ["status"] = "active",
            ["content_kind"] = "image",
            ["payment_account_id"] = Guid.CreateVersion7().ToString("D"),
            ["retention"] = "permanent",
        };
        var file = CreateFormFile("image/jpg", "menu.jpg", [1, 2, 3, 4]);
        var options = new FileUploadValidationOptions(
            RequiredMessage: "File is required.",
            InvalidContentTypeMessage: "File must be a JPEG image.",
            AllowedContentTypes: ["image/jpeg"]);

        FormFileBlobUpload.ValidateRequired(file, options).ShouldBeNull();
        await FormFileBlobUpload.UploadAsync(blobStorage, file, reference, tags, CancellationToken.None);

        var upload = blobStorage.Uploads.Single();
        upload.Reference.ShouldBe(reference);
        upload.ContentType.ShouldBe("image/jpeg");
        upload.Content.ShouldBe([1, 2, 3, 4]);
        upload.Tags.ShouldBe(tags);
    }

    [Test]
    public void ValidateRequired_AcceptsNonImageFilesWhenTheirContentTypeIsAllowed()
    {
        var file = CreateFormFile("application/pdf", "terms.pdf", [1]);
        var options = new FileUploadValidationOptions(
            RequiredMessage: "File is required.",
            InvalidContentTypeMessage: "File must be a PDF.",
            AllowedContentTypes: ["application/pdf"]);

        FormFileBlobUpload.ValidateRequired(file, options).ShouldBeNull();
    }

    [Test]
    public void ValidateRequired_ReturnsConfiguredRequiredMessageWhenFileIsMissing()
    {
        var options = new FileUploadValidationOptions(
            RequiredMessage: "QR image is required.",
            InvalidContentTypeMessage: "QR image must be a PNG or JPEG file.",
            AllowedContentTypes: ["image/png", "image/jpeg"]);

        FormFileBlobUpload.ValidateRequired(file: null, options).ShouldBe("QR image is required.");
    }

    private static FormFile CreateFormFile(string contentType, string fileName, byte[] content)
    {
        return new FormFile(new MemoryStream(content), 0, content.Length, "file", fileName)
        {
            Headers = new HeaderDictionary(),
            ContentType = contentType,
        };
    }

    private sealed class FakeBlobStorageService : IBlobStorageService
    {
        public List<CapturedUpload> Uploads { get; } = [];

        public async Task<BlobStorageUploadResult> UploadAsync(BlobStorageUploadRequest request, CancellationToken cancellationToken)
        {
            await using var memory = new MemoryStream();
            await request.Content.CopyToAsync(memory, cancellationToken);
            Uploads.Add(new CapturedUpload(
                request.Reference,
                memory.ToArray(),
                request.ContentType,
                new Dictionary<string, string>(request.Tags, StringComparer.Ordinal)));
            return new BlobStorageUploadResult(
                request.Reference,
                $"https://storage.test/{request.Reference.ContainerName}/{request.Reference.BlobName}");
        }

        public Task<BlobStorageDownloadResult> DownloadAsync(
            BlobStorageReference reference,
            CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
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

    private sealed record CapturedUpload(
        BlobStorageReference Reference,
        byte[] Content,
        string ContentType,
        IReadOnlyDictionary<string, string> Tags);
}
