using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace WebAPI.Common;

public sealed class FlexibleStringJsonConverter : JsonConverter<string>
{
    public override string Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return reader.TokenType switch
        {
            JsonTokenType.String => reader.GetString() ?? string.Empty,
            JsonTokenType.Number => ReadNumberAsString(ref reader),
            JsonTokenType.True => bool.TrueString,
            JsonTokenType.False => bool.FalseString,
            JsonTokenType.Null => string.Empty,
            _ => throw new JsonException($"Unsupported token type: {reader.TokenType}")
        };
    }

    public override void Write(Utf8JsonWriter writer, string value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value);
    }

    private static string ReadNumberAsString(ref Utf8JsonReader reader)
    {
        if (reader.TryGetInt64(out var longValue))
        {
            return longValue.ToString(CultureInfo.InvariantCulture);
        }

        if (reader.TryGetDecimal(out var decimalValue))
        {
            return decimalValue.ToString(CultureInfo.InvariantCulture);
        }

        var doubleValue = reader.GetDouble();
        return doubleValue.ToString(CultureInfo.InvariantCulture);
    }
}
