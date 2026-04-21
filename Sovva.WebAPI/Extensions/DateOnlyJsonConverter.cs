using System;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Sovva.WebAPI.Extensions;

/// <summary>
/// Handles DateOnly serialization/deserialization for System.Text.Json.
/// .NET 8 does not natively support DateOnly in JSON without this converter.
/// Register in Program.cs via AddJsonOptions.
/// </summary>
public sealed class DateOnlyJsonConverter : JsonConverter<DateOnly>
{
    private const string Format = "yyyy-MM-dd";

    public override DateOnly Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => DateOnly.ParseExact(reader.GetString()!, Format, CultureInfo.InvariantCulture);

    public override void Write(Utf8JsonWriter writer, DateOnly value, JsonSerializerOptions options)
        => writer.WriteStringValue(value.ToString(Format, CultureInfo.InvariantCulture));
}