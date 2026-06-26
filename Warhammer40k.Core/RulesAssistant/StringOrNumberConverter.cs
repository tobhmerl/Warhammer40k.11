using System.Text.Json;
using System.Text.Json.Serialization;

namespace Warhammer40k.Core.RulesAssistant;

/// <summary>
/// Reads a JSON value that may be a string <b>or</b> a number into a string property. Rules corpora vary on
/// whether <c>section</c> is written as <c>"11"</c> or <c>11</c>; this keeps <see cref="RuleCard.Section"/> a
/// clean string either way. Part of the removable Rules Assistant feature.
/// </summary>
public sealed class StringOrNumberConverter : JsonConverter<string>
{
    public override string Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        reader.TokenType switch
        {
            JsonTokenType.String => reader.GetString() ?? "",
            JsonTokenType.Number => reader.TryGetInt64(out var l)
                ? l.ToString(System.Globalization.CultureInfo.InvariantCulture)
                : reader.GetDouble().ToString(System.Globalization.CultureInfo.InvariantCulture),
            JsonTokenType.Null => "",
            _ => "",
        };

    public override void Write(Utf8JsonWriter writer, string value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value);
}
