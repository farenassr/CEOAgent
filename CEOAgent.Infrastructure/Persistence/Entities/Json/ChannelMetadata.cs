using System.Text.Json.Serialization;

namespace CEOAgent.Infrastructure.Persistence.Entities.Json;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "$provider")]
[JsonDerivedType(typeof(WhatsAppCloudMetadata), "whatsapp_cloud")]
[JsonDerivedType(typeof(InstagramMetadata), "instagram")]
[JsonDerivedType(typeof(TelegramMetadata), "telegram")]
public abstract class ChannelMetadata;

public sealed class WhatsAppCloudMetadata : ChannelMetadata
{
    public required string BusinessAccountId { get; set; }

    public required string PhoneNumberId { get; set; }

    public string? DisplayPhoneNumber { get; set; }

    public string? VerifiedName { get; set; }
}

public sealed class InstagramMetadata : ChannelMetadata
{
    public required string IgUserId { get; set; }

    public string? PageId { get; set; }
}

public sealed class TelegramMetadata : ChannelMetadata
{
    public required string BotUsername { get; set; }

    public long ChatId { get; set; }
}
