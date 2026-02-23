using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace WastelandSurvivor.Core.IO;

public static class JsonUtil
{
    public static readonly JsonSerializerOptions Options = CreateOptions();

    private static JsonSerializerOptions CreateOptions()
    {
        var o = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
            WriteIndented = true
        };

        o.Converters.Add(new JsonStringEnumConverter());
        o.Converters.Add(new EnumKeyDictionaryConverterFactory());
        return o;
    }

    public static T? Deserialize<T>(string json)
        => JsonSerializer.Deserialize<T>(json, Options);

    /// <summary>
    /// Supports Dictionary&lt;TEnum, TValue&gt; where enum keys are JSON object property names.
    /// Example: { "Front": 10, "Rear": 8 }
    /// </summary>
    private sealed class EnumKeyDictionaryConverterFactory : JsonConverterFactory
    {
        public override bool CanConvert(Type typeToConvert)
        {
            if (!typeToConvert.IsGenericType) return false;
            if (typeToConvert.GetGenericTypeDefinition() != typeof(Dictionary<,>)) return false;

            var args = typeToConvert.GetGenericArguments();
            return args[0].IsEnum; // key is enum
        }

        public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options)
        {
            var args = typeToConvert.GetGenericArguments();
            var keyType = args[0];
            var valueType = args[1];

            var converterType = typeof(EnumKeyDictionaryConverter<,>).MakeGenericType(keyType, valueType);
            return (JsonConverter)Activator.CreateInstance(converterType)!;
        }
    }

    private sealed class EnumKeyDictionaryConverter<TKeyEnum, TValue> : JsonConverter<Dictionary<TKeyEnum, TValue>>
        where TKeyEnum : struct, Enum
    {
        public override Dictionary<TKeyEnum, TValue> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.StartObject)
                throw new JsonException("Expected object for enum-key dictionary.");

            var dict = new Dictionary<TKeyEnum, TValue>();

            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.EndObject)
                    return dict;

                if (reader.TokenType != JsonTokenType.PropertyName)
                    throw new JsonException("Expected property name.");

                var keyString = reader.GetString() ?? throw new JsonException("Null dictionary key.");
                if (!Enum.TryParse<TKeyEnum>(keyString, ignoreCase: true, out var key))
                    throw new JsonException($"Invalid enum key '{keyString}' for {typeof(TKeyEnum).Name}.");

                reader.Read();
                var value = JsonSerializer.Deserialize<TValue>(ref reader, options)!;
                dict[key] = value;
            }

            throw new JsonException("Unexpected end of JSON while reading dictionary.");
        }

        public override void Write(Utf8JsonWriter writer, Dictionary<TKeyEnum, TValue> value, JsonSerializerOptions options)
        {
            writer.WriteStartObject();
            foreach (var kv in value)
            {
                writer.WritePropertyName(kv.Key.ToString());
                JsonSerializer.Serialize(writer, kv.Value, options);
            }
            writer.WriteEndObject();
        }
    }
}
