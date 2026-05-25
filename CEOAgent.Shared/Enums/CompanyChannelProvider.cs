using System.Text.Json.Serialization;

namespace CeoAgent.Shared.Enums;

[JsonConverter(typeof(JsonStringEnumConverter<CompanyChannelProvider>))]
public enum CompanyChannelProvider
{
    [JsonStringEnumMemberName("whatsapp_cloud")]
    WhatsAppCloud = 1,

    [JsonStringEnumMemberName("instagram")]
    Instagram = 2,

    [JsonStringEnumMemberName("telegram")]
    Telegram = 3,
}
