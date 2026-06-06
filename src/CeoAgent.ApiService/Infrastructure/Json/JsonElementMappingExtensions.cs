using System.Text.Json;

namespace CeoAgent.ApiService.Infrastructure.Json;

internal static class JsonElementMappingExtensions
{
    private static readonly JsonSerializerOptions SerializerOptions = CreateOptions();

    public static TDocument? DeserializeOptional<TDocument>(this JsonElement? element)
    {
        if (element is not { ValueKind: not JsonValueKind.Null and not JsonValueKind.Undefined } value)
        {
            return default;
        }

        return value.Deserialize<TDocument>(SerializerOptions);
    }

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.MakeReadOnly(populateMissingResolver: true);
        return options;
    }
}
