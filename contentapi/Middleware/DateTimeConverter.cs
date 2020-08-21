using System;
using System.Globalization;
//using System.Text.Json;
//using System.Text.Json.Serialization;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace contentapi
{
    ////Taken directly from https://stackoverflow.com/a/58284103/1066474
    ///// <summary>
    ///// Makes timespans display properly in views, configured in startup
    ///// </summary>
    //public class DateTimeConverter : JsonConverter<DateTime>
    //{
    //    public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    //    {
    //        var value = reader.GetString();
    //        return DateTime.Parse(value, CultureInfo.InvariantCulture); //TimeSpan.Parse(value, CultureInfo.InvariantCulture);
    //    }

    //    public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
    //    {
    //        writer.WriteStringValue(value.ToUniversalTime().ToString("YYYY-MM-ddTHH:mm:ss.ffZ", CultureInfo.InvariantCulture));
    //    }
    //}

    /// <summary>
    /// Custom DateTime JSON serializer/deserializer
    /// </summary>
    /// <remarks>Taken mostly from https://blog.kulman.sk/custom-datetime-deserialization-with-json-net/</remarks>
    public class CustomDateTimeConverter :  DateTimeConverterBase
    {
        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            writer.WriteValue(((DateTime)value).ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss.ffZ", CultureInfo.InvariantCulture));
        }

        public override bool CanRead => false;

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            throw new NotImplementedException("Shouldn't be reading");
        }
    }
}