using System;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace contentapi
{
    //Taken directly from https://stackoverflow.com/a/58284103/1066474
    public class TimeSpanToStringConverter : JsonConverter<TimeSpan>
    {
        public override TimeSpan Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            var value = reader.GetString();
            return TimeSpan.Parse(value, CultureInfo.InvariantCulture);
        }

        public override void Write(Utf8JsonWriter writer, TimeSpan value, JsonSerializerOptions options)
        {
            writer.WriteStringValue(value.ToString(null, CultureInfo.InvariantCulture));
        }
    }
}