using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace WorkerTemplate.Converters
{
    public sealed class CanonicalDateTimeOffsetConverter : JsonConverter<DateTimeOffset?>
    {
        private const string Format = "yyyy-MM-dd'T'HH:mm:ss.fffK";

        private static readonly Regex TimezoneRegex =
            new(@"([+-]\d{2})(\d{2})$", RegexOptions.Compiled);

        public override DateTimeOffset? Read(
            ref Utf8JsonReader reader,
            Type typeToConvert,
            JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.Null)
                return null;

            var value = reader.GetString();

            if (string.IsNullOrWhiteSpace(value))
                return null;

            // Convert +0800 -> +08:00
            value = TimezoneRegex.Replace(value, "$1:$2");

            if (DateTimeOffset.TryParse(
                    value,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.None,
                    out var result))
            {
                return result;
            }

            throw new JsonException($"Unable to parse datetime: {value}");
        }

        public override void Write(
            Utf8JsonWriter writer,
            DateTimeOffset? value,
            JsonSerializerOptions options)
        {
            if (value is null)
            {
                writer.WriteNullValue();
                return;
            }

            writer.WriteStringValue(value.Value.ToString(Format));
        }
    }
}