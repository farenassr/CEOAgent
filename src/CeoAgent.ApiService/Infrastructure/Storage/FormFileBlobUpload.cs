using CeoAgent.Application.Abstractions.Storage;
using CeoAgent.Shared.Storage;
using Microsoft.AspNetCore.Http;

namespace CeoAgent.ApiService.Infrastructure.Storage;

public static class FormFileBlobUpload
{
    public static IFormFile? GetFile(IFormFileCollection files, string formFieldName)
    {
        ArgumentNullException.ThrowIfNull(files);
        ArgumentException.ThrowIfNullOrWhiteSpace(formFieldName);

        return files.GetFile(formFieldName);
    }

    public static string? ValidateRequired(IFormFile? file, FileUploadValidationOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (file is null || file.Length == 0)
        {
            return options.RequiredMessage;
        }

        var contentType = NormalizeContentType(file.ContentType);
        if (string.IsNullOrWhiteSpace(contentType)
            || !options.AllowedContentTypes
                .Select(NormalizeContentType)
                .Contains(contentType, StringComparer.Ordinal))
        {
            return options.InvalidContentTypeMessage;
        }

        return null;
    }

    public static async Task<BlobStorageUploadResult> UploadAsync(
        IBlobStorageService blobStorage,
        IFormFile file,
        BlobStorageReference reference,
        IReadOnlyDictionary<string, string> tags,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(blobStorage);
        ArgumentNullException.ThrowIfNull(file);
        ArgumentNullException.ThrowIfNull(reference);
        ArgumentNullException.ThrowIfNull(tags);

        await using var stream = file.OpenReadStream();
        return await blobStorage.UploadAsync(
            new BlobStorageUploadRequest(
                reference,
                stream,
                NormalizeContentType(file.ContentType),
                tags),
            cancellationToken);
    }

    private static string NormalizeContentType(string contentType)
    {
        if (string.Equals(contentType, "image/jpg", StringComparison.OrdinalIgnoreCase))
        {
            return "image/jpeg";
        }

        return contentType.Trim().ToLowerInvariant();
    }
}
