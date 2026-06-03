using System.Text.Json.Serialization;

namespace CeoAgent.Shared.Enums;

[JsonConverter(typeof(JsonStringEnumConverter<LlmProvider>))]
public enum LlmProvider
{
    [JsonStringEnumMemberName("openai")]
    OpenAI = 1,
}
