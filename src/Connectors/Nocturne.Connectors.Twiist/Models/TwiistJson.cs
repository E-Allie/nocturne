using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Nocturne.Connectors.Twiist.Models;

public static class TwiistJson
{
    public static JsonSerializerOptions Options { get; } = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };
}

public sealed class TwiistNullableDecimalConverter : JsonConverter<decimal?>
{
    public override decimal? Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
            return null;

        if (reader.TokenType == JsonTokenType.Number)
            return reader.GetDecimal();

        if (reader.TokenType == JsonTokenType.String)
        {
            var value = reader.GetString();
            if (string.IsNullOrWhiteSpace(value))
                return null;

            if (decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed))
                return parsed;
        }

        throw new JsonException($"Cannot parse {reader.TokenType} as nullable decimal.");
    }

    public override void Write(
        Utf8JsonWriter writer,
        decimal? value,
        JsonSerializerOptions options)
    {
        if (value.HasValue)
            writer.WriteStringValue(value.Value.ToString(CultureInfo.InvariantCulture));
        else
            writer.WriteNullValue();
    }
}
