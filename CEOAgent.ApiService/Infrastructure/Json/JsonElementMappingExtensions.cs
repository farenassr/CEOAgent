using System.Text.Json;

namespace CeoAgent.ApiService.Infrastructure.Json;

internal static class JsonElementMappingExtensions
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public static TDocument? DeserializeOptional<TDocument>(this JsonElement? element)
    {
        if (element is not { ValueKind: not JsonValueKind.Null and not JsonValueKind.Undefined } value)
        {
            return default;
        }

        return value.Deserialize<TDocument>(SerializerOptions);
    }
}
