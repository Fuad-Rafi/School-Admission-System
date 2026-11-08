using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Admission.Api.Infrastructure.Json;

/// <summary>
/// Serialises timestamps as UTC ISO-8601 with a trailing <c>Z</c>, matching the API contract
/// (e.g. <c>2026-11-01T04:10:00Z</c>).
/// </summary>
public sealed class UtcDateTimeOffsetConverter : JsonConverter<DateTimeOffset>
{
    private const string Format = "yyyy-MM-dd'T'HH:mm:ss'Z'";

    public override DateTimeOffset Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        var text = reader.GetString()
            ?? throw new JsonException("Expected a timestamp string.");

        return DateTimeOffset.Parse(
            text,
            CultureInfo.InvariantCulture,
            DateTimeStyles.RoundtripKind);
    }

    public override void Write(
        Utf8JsonWriter writer,
        DateTimeOffset value,
        JsonSerializerOptions options)
    {
        writer.WriteStringValue(
            value.ToUniversalTime().ToString(Format, CultureInfo.InvariantCulture));
    }
}
