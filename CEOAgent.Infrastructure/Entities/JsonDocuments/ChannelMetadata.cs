using System.Text.Json.Serialization;

namespace CEOAgent.Infrastructure.Entities.JsonDocuments;

public sealed class ChannelMetadata
{
    [JsonPropertyName("whatsapp_cloud")]
    public WhatsAppCloudMetadata? WhatsAppCloud { get; set; }

    [JsonPropertyName("instagram")]
    public InstagramMetadata? Instagram { get; set; }

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
    [JsonPropertyName("business_account_id")]
    public required string BusinessAccountId { get; set; }

    [JsonPropertyName("phone_number_id")]
    public required string PhoneNumberId { get; set; }

    [JsonPropertyName("display_phone_number")]
    public string? DisplayPhoneNumber { get; set; }

    [JsonPropertyName("verified_name")]
    public string? VerifiedName { get; set; }
}

public sealed class InstagramMetadata
{
    [JsonPropertyName("ig_user_id")]
    public required string IgUserId { get; set; }

    [JsonPropertyName("page_id")]
    public string? PageId { get; set; }
}

public sealed class TelegramMetadata
{
    [JsonPropertyName("bot_username")]
    public required string BotUsername { get; set; }

    [JsonPropertyName("chat_id")]
    public long ChatId { get; set; }
}
