using System.Globalization;
using System.Text;

namespace CeoAgent.Shared.Storage;

public static class BlobStorageNaming
{
    public static BlobStorageReference ForConversationMedia(
        string organizationName,
        Guid organizationId,
        Guid conversationId,
        Guid messageId,
        Guid assetId,
        string extension)
    {
        var normalizedExtension = NormalizeExtension(extension);
        return BlobStorageReference.Create(
            BlobStorageContainerNames.Private,
            $"organizations/{OrganizationSegment(organizationName, organizationId)}/conversations/{conversationId:D}/messages/{messageId:D}/media/{assetId:D}{normalizedExtension}");
    }

    public static BlobStorageReference ForPaymentQr(string fileName, Guid paymentAccountId)
    {
        var extension = NormalizeExtension(Path.GetExtension(fileName));
        var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(fileName);
        return BlobStorageReference.Create(
            BlobStorageContainerNames.Private,
            $"{FileNameSlug(fileNameWithoutExtension)}-{paymentAccountId:D}{extension}");
    }

    public static BlobStorageReference ForPublicAsset(
        string organizationName,
        Guid organizationId,
        Guid assetId)
    {
        return BlobStorageReference.Create(
            BlobStorageContainerNames.Public,
            $"organizations/{OrganizationSegment(organizationName, organizationId)}/assets/{assetId:D}");
    }

    public static string OrganizationSlug(string organizationName)
    {
        var normalized = organizationName
            .Trim()
            .Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(normalized.Length);
        var pendingSeparator = false;

        foreach (var character in normalized)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(character);
            if (category == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            var lower = char.ToLowerInvariant(character);
            if (lower is >= 'a' and <= 'z' or >= '0' and <= '9')
            {
                if (pendingSeparator && builder.Length > 0)
                {
                    builder.Append('-');
                }

                builder.Append(lower);
                pendingSeparator = false;
                continue;
            }

            pendingSeparator = builder.Length > 0;
        }

        return builder.Length == 0
            ? "organization"
            : builder.ToString();
    }

    private static string OrganizationSegment(string organizationName, Guid organizationId)
    {
        return $"{OrganizationSlug(organizationName)}-{organizationId:D}";
    }

    private static string FileNameSlug(string fileName)
    {
        return string.IsNullOrWhiteSpace(fileName)
            ? "file"
            : OrganizationSlug(fileName);
    }

    private static string NormalizeExtension(string extension)
    {
        if (string.IsNullOrWhiteSpace(extension))
        {
            throw new ArgumentException("Blob extension is required.", nameof(extension));
        }

        var normalized = extension.Trim();
        if (!normalized.StartsWith('.'))
        {
            normalized = $".{normalized}";
        }

        if (normalized.Contains('/')
            || normalized.Contains('\\')
            || normalized.Contains(' '))
        {
            throw new ArgumentException("Blob extension must be a simple file extension.", nameof(extension));
        }

        return normalized.ToLowerInvariant();
    }
}
