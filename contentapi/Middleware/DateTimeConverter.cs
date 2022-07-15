using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace contentapi;

public class DateTimeJsonConverter : JsonConverter<DateTime>
{
    public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        DateTime.Parse(reader.GetString()!);

    public override void Write( Utf8JsonWriter writer, DateTime dateTimeValue, JsonSerializerOptions options) =>
            //writer.WriteStringValue(dateTimeValue.ToString("MM/dd/yyyy", CultureInfo.InvariantCulture));
        writer.WriteStringValue(dateTimeValue.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss.ffZ", CultureInfo.InvariantCulture));
}