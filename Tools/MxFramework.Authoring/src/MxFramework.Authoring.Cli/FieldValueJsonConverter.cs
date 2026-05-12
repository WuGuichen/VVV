using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using MxFramework.Authoring;

namespace MxFramework.Authoring.Cli;

internal sealed class FieldValueJsonConverter : JsonConverter<FieldValue>
{
    public override FieldValue Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return ReadValue(ref reader);
    }

    private static FieldValue ReadValue(ref Utf8JsonReader reader)
    {
        switch (reader.TokenType)
        {
            case JsonTokenType.String:
                return FieldValue.FromScalar(reader.GetString() ?? string.Empty);
            case JsonTokenType.Number:
                if (reader.TryGetInt64(out long iv))
                    return FieldValue.FromScalar(iv.ToString(CultureInfo.InvariantCulture));
                if (reader.TryGetDouble(out double dv))
                    return FieldValue.FromScalar(dv.ToString("R", CultureInfo.InvariantCulture));
                return FieldValue.FromScalar(reader.GetDecimal().ToString(CultureInfo.InvariantCulture));
            case JsonTokenType.True:
                return FieldValue.FromScalar("true");
            case JsonTokenType.False:
                return FieldValue.FromScalar("false");
            case JsonTokenType.Null:
                return FieldValue.FromScalar(string.Empty);
            case JsonTokenType.StartArray:
            {
                var fv = new FieldValue { Kind = FieldValueKind.List, List = new List<FieldValue>() };
                while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
                    fv.List.Add(ReadValue(ref reader));
                return fv;
            }
            case JsonTokenType.StartObject:
            {
                var fv = new FieldValue { Kind = FieldValueKind.Map, Map = new Dictionary<string, FieldValue>() };
                while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
                {
                    string key = reader.GetString() ?? string.Empty;
                    reader.Read();
                    fv.Map[key] = ReadValue(ref reader);
                }
                return fv;
            }
            default:
                throw new JsonException("Unsupported FieldValue token: " + reader.TokenType);
        }
    }

    public override void Write(Utf8JsonWriter writer, FieldValue value, JsonSerializerOptions options)
    {
        WriteValue(writer, value);
    }

    private static void WriteValue(Utf8JsonWriter writer, FieldValue value)
    {
        if (value == null)
        {
            writer.WriteStringValue(string.Empty);
            return;
        }

        if (value.Kind == FieldValueKind.Scalar)
        {
            writer.WriteStringValue(value.Scalar ?? string.Empty);
            return;
        }

        if (value.Kind == FieldValueKind.List)
        {
            writer.WriteStartArray();
            if (value.List != null)
            {
                for (int i = 0; i < value.List.Count; i++)
                    WriteValue(writer, value.List[i]);
            }
            writer.WriteEndArray();
            return;
        }

        writer.WriteStartObject();
        if (value.Map != null)
        {
            foreach (KeyValuePair<string, FieldValue> kv in value.Map)
            {
                writer.WritePropertyName(kv.Key);
                WriteValue(writer, kv.Value);
            }
        }
        writer.WriteEndObject();
    }
}
