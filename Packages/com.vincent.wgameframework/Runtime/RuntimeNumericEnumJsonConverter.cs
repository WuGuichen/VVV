using System;
using System.Globalization;
using Newtonsoft.Json;

namespace MxFramework.Runtime
{
    /// <summary>Low-frequency wire decoding of explicitly declared Int32 enum identities.</summary>
    public sealed class RuntimeNumericEnumJsonConverter<T> : JsonConverter where T : struct, Enum
    {
        public override bool CanConvert(Type objectType) => objectType == typeof(T);

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            if (value is not T typed || !Enum.IsDefined(typeof(T), typed))
                throw new JsonSerializationException("Undefined numeric enum identity.");
            writer.WriteValue(Convert.ToInt32(typed, CultureInfo.InvariantCulture));
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            if (reader.TokenType != JsonToken.Integer)
                throw new JsonSerializationException("Enum identities must be JSON integers.");
            int number;
            try { number = Convert.ToInt32(reader.Value, CultureInfo.InvariantCulture); }
            catch (Exception exception) when (exception is OverflowException || exception is InvalidCastException || exception is FormatException)
            { throw new JsonSerializationException("Enum identity is outside Int32 range.", exception); }
            if (!Enum.IsDefined(typeof(T), number))
                throw new JsonSerializationException("Undefined numeric enum identity.");
            return (T)Enum.ToObject(typeof(T), number);
        }
    }
}
