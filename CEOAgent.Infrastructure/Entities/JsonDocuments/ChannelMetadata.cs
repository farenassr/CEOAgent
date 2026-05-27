using System.Text.Json.Serialization;

namespace CeoAgent.Infrastructure.Entities.JsonDocuments;

public sealed class ChannelMetadata
{
    /// <summary>
    /// WhatsApp Cloud-specific channel metadata when the channel provider is WhatsApp Cloud.
    /// </summary>
    [JsonPropertyName("whatsapp_cloud")]
    public WhatsAppCloudMetadata? WhatsAppCloud { get; set; }

    /// <summary>
    /// Instagram-specific channel metadata when the channel provider is Instagram.
    /// </summary>
    [JsonPropertyName("instagram")]
    public InstagramMetadata? Instagram { get; set; }

    /// <summary>
    /// Telegram-specific channel metadata when the channel provider is Telegram.
    /// </summary>
    [JsonPropertyName("telegram")]
    public TelegramMetadata? Telegram { get; set; }

    public static ChannelMetadata ForWhatsAppCloud(WhatsAppCloudMetadata metadata)
    {
        ArgumentNullException.ThrowIfNull(metadata);

        return new ChannelMetadata
        {
            WhatsAppCloud = metadata,
        };
    }

    public static ChannelMetadata ForInstagram(InstagramMetadata metadata)
    {
        throw new NotSupportedException("Instagram channel metadata is not supported in the MVP.");
    }

    public static ChannelMetadata ForTelegram(TelegramMetadata metadata)
    {
        throw new NotSupportedException("Telegram channel metadata is not supported in the MVP.");
    }
}

public sealed class WhatsAppCloudMetadata
{
    /// <summary>
    /// WhatsApp Business Account identifier that owns the phone number.
    /// </summary>
    [JsonPropertyName("business_account_id")]
    public required string BusinessAccountId { get; set; }

    /// <summary>
    /// WhatsApp Cloud phone number identifier used to resolve inbound webhooks.
    /// </summary>
    [JsonPropertyName("phone_number_id")]
    public required string PhoneNumberId { get; set; }

    /// <summary>
    /// Human-readable phone number shown by WhatsApp when available.
    /// </summary>
    [JsonPropertyName("display_phone_number")]
    public string? DisplayPhoneNumber { get; set; }

    /// <summary>
    /// Verified WhatsApp business display name when available.
    /// </summary>
    [JsonPropertyName("verified_name")]
    public string? VerifiedName { get; set; }
}

public sealed class InstagramMetadata
{
    /// <summary>
    /// Instagram user identifier for the business account.
    /// </summary>
    [JsonPropertyName("ig_user_id")]
    public required string IgUserId { get; set; }

    /// <summary>
    /// Optional Facebook page identifier connected to the Instagram account.
    /// </summary>
    [JsonPropertyName("page_id")]
    public string? PageId { get; set; }
}

public sealed class TelegramMetadata
{
    /// <summary>
    /// Telegram bot username for the channel.
    /// </summary>
    [JsonPropertyName("bot_username")]
    public required string BotUsername { get; set; }

    /// <summary>
    /// Telegram chat identifier associated with the channel.
    /// </summary>
    [JsonPropertyName("chat_id")]
    public long ChatId { get; set; }
}
