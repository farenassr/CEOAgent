using System.Text.Json.Serialization;

namespace CeoAgent.Shared.Enums;

[JsonConverter(typeof(JsonStringEnumConverter<IntegrationProvider>))]
public enum IntegrationProvider
{
    [JsonStringEnumMemberName("whatsapp_cloud")]
    WhatsAppCloud = 1,

    [JsonStringEnumMemberName("google_calendar")]
    GoogleCalendar = 2,
}
