using System;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace WorkerTemplate.Converters
{
    /// <summary>
    /// Forces DateTimeOffset serialization to strictly use the UTC 'Z' format 
    /// instead of the standard '+00:00' offset.
    /// </summary>
    public sealed class IsoUtcDateTimeOffsetConverter : JsonConverter<DateTimeOffset>
    {
        // 'Z' is explicitly escaped inside single quotes so C# treats it as a literal string character
        private const string IsoUtcFormat = "yyyy-MM-dd'T'HH:mm:ss.fff'Z'";

        public override DateTimeOffset Read(
            ref Utf8JsonReader reader,
            Type typeToConvert,
            JsonSerializerOptions options)
        {
            return reader.GetDateTimeOffset();
        }

        public override void Write(
            Utf8JsonWriter writer,
            DateTimeOffset value,
            JsonSerializerOptions options)
        {
            string formattedDate = value.ToUniversalTime().ToString(IsoUtcFormat, CultureInfo.InvariantCulture);

            writer.WriteStringValue(formattedDate);
        }
    }
}