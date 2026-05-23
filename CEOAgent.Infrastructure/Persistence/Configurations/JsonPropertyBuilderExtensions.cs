using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace CEOAgent.Infrastructure.Persistence.Configurations;

internal static class JsonPropertyBuilderExtensions
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    public static PropertyBuilder<TProperty> HasJsonbConversion<TProperty>(
        this PropertyBuilder<TProperty> builder,
        string columnName)
    {
        var converter = new ValueConverter<TProperty, string?>(
            value => Serialize(value),
            value => Deserialize<TProperty>(value)!);

        var comparer = new ValueComparer<TProperty>(
            (left, right) => AreEqual(left, right),
            value => GetJsonHashCode(value),
            value => Clone(value)!);

        return builder
            .HasConversion(converter, comparer)
            .HasColumnName(columnName)
            .HasColumnType("jsonb");
    }

    private static string? Serialize<TValue>(TValue value)
    {
        return value is null ? null : JsonSerializer.Serialize(value, JsonOptions);
    }

    private static TValue? Deserialize<TValue>(string? value)
    {
        return value is null ? default : JsonSerializer.Deserialize<TValue>(value, JsonOptions);
    }

    private static bool AreEqual<TValue>(TValue left, TValue right)
    {
        return Serialize(left) == Serialize(right);
    }

    private static int GetJsonHashCode<TValue>(TValue value)
    {
        var json = Serialize(value);
        return json is null ? 0 : StringComparer.Ordinal.GetHashCode(json);
    }

    private static TValue? Clone<TValue>(TValue value)
    {
        var json = Serialize(value);
        return Deserialize<TValue>(json);
    }
}
