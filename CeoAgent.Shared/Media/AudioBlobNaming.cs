using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using CeoAgent.Shared.Enums;

namespace CeoAgent.Shared.Media;

/// <summary>
/// Builds deterministic blob names and metadata for inbound and outbound audio media.
/// </summary>
public static partial class AudioBlobNaming
{
    /// <summary>
    /// Creates a stable audio blob path using the company slug, direction, UTC date, and message identifier.
    /// </summary>
    public static string CreatePath(
        string companyNameOrSlug,
        Guid companyId,
        AudioBlobDirection direction,
        DateTimeOffset createdAtUtc,
        Guid messageId,
        string extension)
    {
        var companySlug = Slugify(companyNameOrSlug);
        var normalizedExtension = NormalizeExtension(extension);
        var directionSegment = ToStorageValue(direction);

        return string.Create(
            CultureInfo.InvariantCulture,
            $"companies/{companySlug}-{companyId}/media/audio/{directionSegment}/{createdAtUtc:yyyy-MM-dd}/{messageId}{normalizedExtension}");
    }

    /// <summary>
    /// Converts an audio blob request into Azure Blob Storage metadata with normalized direction and extension values.
    /// </summary>
    public static Dictionary<string, string> CreateMetadata(AudioBlobMetadataRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var metadata = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["company_id"] = request.CompanyId.ToString(),
            ["company_slug"] = request.CompanySlug,
            ["conversation_id"] = request.ConversationId.ToString(),
            ["message_id"] = request.MessageId.ToString(),
            ["customer_id"] = request.CustomerId.ToString(),
            ["direction"] = ToStorageValue(request.Direction),
            ["provider"] = request.Provider,
            ["content_type"] = request.ContentType,
            ["original_extension"] = NormalizeExtension(request.OriginalExtension),
            ["created_at_utc"] = request.CreatedAtUtc.UtcDateTime.ToString("O", CultureInfo.InvariantCulture),
        };

        if (!string.IsNullOrWhiteSpace(request.ProviderMediaId))
        {
            metadata["provider_media_id"] = request.ProviderMediaId.Trim();
        }

        return metadata;
    }

    /// <summary>
    /// Normalizes arbitrary company text into a lowercase, dash-separated storage-safe slug.
    /// </summary>
    public static string Slugify(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "company";
        }

        var normalized = value.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(normalized.Length);
        foreach (var character in normalized)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(character);
            if (category == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            if (char.IsLetterOrDigit(character))
            {
                builder.Append(char.ToLowerInvariant(character));
                continue;
            }

            builder.Append('-');
        }

        var slug = DuplicateDashesRegex().Replace(builder.ToString(), "-").Trim('-');
        return slug.Length == 0 ? "company" : slug;
    }

    private static string NormalizeExtension(string extension)
    {
        if (string.IsNullOrWhiteSpace(extension))
        {
            return ".bin";
        }

        var trimmed = extension.Trim();
        return trimmed.StartsWith('.') ? trimmed : "." + trimmed;
    }

    private static string ToStorageValue(AudioBlobDirection direction)
    {
        return direction switch
        {
            AudioBlobDirection.Inbound => "inbound",
            AudioBlobDirection.Outbound => "outbound",
            _ => throw new ArgumentOutOfRangeException(nameof(direction), direction, "Unsupported audio blob direction."),
        };
    }

    [GeneratedRegex("-+", RegexOptions.None, 1000)]
    private static partial Regex DuplicateDashesRegex();
}
