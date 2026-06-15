using System.Globalization;

namespace CeoAgent.Shared.Storage;

public static class BlobStorageTags
{
    private static readonly HashSet<string> AllowedKeys = new(StringComparer.Ordinal)
    {
        "organization_id",
        "visibility",
        "category",
        "status",
        "content_kind",
        "retention",
        "source",
        "created_ym",
        "payment_account_id",
        "asset_purpose",
    };

    private static readonly Dictionary<string, HashSet<string>> AllowedValues = new(StringComparer.Ordinal)
    {
        ["visibility"] = ["private", "public"],
        ["category"] = ["conversation_media", "payment_qr", "public_asset"],
        ["status"] = ["active", "archived", "deleted", "quarantined"],
        ["content_kind"] = ["image", "audio", "document", "pdf"],
        ["retention"] = ["short", "standard", "legal_hold", "permanent"],
        ["source"] = ["whatsapp_cloud"],
        ["asset_purpose"] = ["logo", "menu", "gallery", "document"],
    };

    public static IReadOnlyDictionary<string, string> ForConversationMedia(
        Guid organizationId,
        string contentKind,
        DateTimeOffset createdAt)
    {
        var tags = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["organization_id"] = organizationId.ToString("D"),
            ["visibility"] = "private",
            ["category"] = "conversation_media",
            ["status"] = "active",
            ["content_kind"] = contentKind,
            ["source"] = "whatsapp_cloud",
            ["created_ym"] = createdAt.UtcDateTime.ToString("yyyyMM", CultureInfo.InvariantCulture),
        };
        Validate(tags);
        return tags;
    }

    public static IReadOnlyDictionary<string, string> ForPaymentQr(Guid organizationId, Guid paymentAccountId)
    {
        var tags = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["organization_id"] = organizationId.ToString("D"),
            ["visibility"] = "private",
            ["category"] = "payment_qr",
            ["status"] = "active",
            ["content_kind"] = "image",
            ["payment_account_id"] = paymentAccountId.ToString("D"),
            ["retention"] = "permanent",
        };
        Validate(tags);
        return tags;
    }

    public static IReadOnlyDictionary<string, string> ForPublicAsset(
        Guid organizationId,
        string contentKind,
        string assetPurpose)
    {
        var tags = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["organization_id"] = organizationId.ToString("D"),
            ["visibility"] = "public",
            ["category"] = "public_asset",
            ["status"] = "active",
            ["content_kind"] = contentKind,
            ["asset_purpose"] = assetPurpose,
        };
        Validate(tags);
        return tags;
    }

    public static void Validate(IReadOnlyDictionary<string, string> tags)
    {
        ArgumentNullException.ThrowIfNull(tags);

        if (tags.Count > 10)
        {
            throw new ArgumentException("Azure Blob index tags support at most 10 tags.", nameof(tags));
        }

        foreach (var (key, value) in tags)
        {
            if (!AllowedKeys.Contains(key))
            {
                throw new ArgumentException($"Blob index tag '{key}' is not allowed.", nameof(tags));
            }

            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException($"Blob index tag '{key}' must have a value.", nameof(tags));
            }

            ValidateDynamicValue(key, value, tags);
        }
    }

    private static void ValidateDynamicValue(
        string key,
        string value,
        IReadOnlyDictionary<string, string> tags)
    {
        if (AllowedValues.TryGetValue(key, out var allowedValues)
            && !allowedValues.Contains(value))
        {
            throw new ArgumentException($"Blob index tag '{key}' has an unsupported value.", nameof(tags));
        }

        if ((key is "organization_id" or "payment_account_id") && !Guid.TryParse(value, out _))
        {
            throw new ArgumentException($"Blob index tag '{key}' must be a GUID.", nameof(tags));
        }

        if (key == "created_ym"
            && (value.Length != 6 || !int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out _)))
        {
            throw new ArgumentException("Blob index tag 'created_ym' must use yyyyMM format.", nameof(tags));
        }
    }
}
