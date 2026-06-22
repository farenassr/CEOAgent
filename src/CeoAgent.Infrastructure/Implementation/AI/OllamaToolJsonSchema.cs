using System.Text.Json;
using System.Text.Json.Nodes;

namespace CeoAgent.Infrastructure.Implementation.AI;

internal static class OllamaToolJsonSchema
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public static JsonElement Normalize(JsonElement schema)
    {
        var node = JsonNode.Parse(schema.GetRawText()) ?? new JsonObject();
        NormalizeNode(node);

        using var document = JsonDocument.Parse(node.ToJsonString(SerializerOptions));
        return document.RootElement.Clone();
    }

    private static bool NormalizeNode(JsonNode? node)
    {
        if (node is JsonArray array)
        {
            foreach (var item in array)
            {
                NormalizeNode(item);
            }

            return false;
        }

        if (node is not JsonObject schema)
        {
            return false;
        }

        var isNullable = NormalizeType(schema);
        foreach (var property in schema.ToArray())
        {
            if (property.Key == "type")
            {
                continue;
            }

            if (property.Key == "properties" && property.Value is JsonObject properties)
            {
                NormalizeProperties(schema, properties);
                continue;
            }

            NormalizeNode(property.Value);
        }

        return isNullable;
    }

    private static bool NormalizeType(JsonObject schema)
    {
        if (schema["type"] is not JsonArray types)
        {
            return false;
        }

        var isNullable = false;
        string? nonNullType = null;
        foreach (var type in types)
        {
            if (type is not JsonValue value || !value.TryGetValue<string>(out var name))
            {
                continue;
            }

            if (string.Equals(name, "null", StringComparison.Ordinal))
            {
                isNullable = true;
                continue;
            }

            nonNullType ??= name;
        }

        if (isNullable)
        {
            schema["type"] = nonNullType ?? "string";
        }

        return isNullable;
    }

    private static void NormalizeProperties(JsonObject schema, JsonObject properties)
    {
        var nullableProperties = new HashSet<string>(StringComparer.Ordinal);
        foreach (var property in properties.ToArray())
        {
            if (NormalizeNode(property.Value))
            {
                nullableProperties.Add(property.Key);
            }
        }

        if (nullableProperties.Count == 0 || schema["required"] is not JsonArray required)
        {
            return;
        }

        var normalizedRequired = new JsonArray();
        foreach (var item in required)
        {
            if (item is JsonValue value
                && value.TryGetValue<string>(out var propertyName)
                && !nullableProperties.Contains(propertyName))
            {
                normalizedRequired.Add(propertyName);
            }
        }

        schema["required"] = normalizedRequired;
    }
}
