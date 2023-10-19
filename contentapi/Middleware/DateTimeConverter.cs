using System.Globalization;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace contentapi;

/// <summary>
/// Custom DateTime JSON serializer/deserializer
/// </summary>
/// <remarks>Taken mostly from https://blog.kulman.sk/custom-datetime-deserialization-with-json-net/</remarks>
public class CustomDateTimeConverter : DateTimeConverterBase
{
    public override void WriteJson(JsonWriter writer, object? value, JsonSerializer serializer)
    {
        if(value == null)
            writer.WriteValue((string?)null);
        else
            writer.WriteValue(Constants.ToCommonDateString((DateTime)value));
    }

    public override bool CanRead => false;

    public override object ReadJson(JsonReader reader, Type objectType, object? existingValue, JsonSerializer serializer)
    {
        throw new NotImplementedException("Shouldn't be reading");
    }
}