using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Enterprise.Patterns.Result;

/// <summary>JSON converter for non-generic <see cref="Result"/> using System.Text.Json.</summary>
public sealed class ResultJsonConverter : JsonConverter<Result>
{
    /// <summary>Reads a <see cref="Result"/> from JSON.</summary>
    /// <param name="reader">The UTF-8 JSON reader.</param>
    /// <param name="typeToConvert">The type to convert.</param>
    /// <param name="options">Serializer options.</param>
    /// <returns>The deserialized result.</returns>
    public override Result Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartObject)
        {
            throw new JsonException("Expected StartObject token.");
        }

        bool isSuccess = false;
        List<Error>? errors = null;

        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject)
            {
                return isSuccess ? Result.Success() : Result.Failure(errors ?? [Error.Uninitialized]);
            }

            if (reader.TokenType != JsonTokenType.PropertyName)
            {
                throw new JsonException("Expected PropertyName token.");
            }

            string? propName = reader.GetString();
            reader.Read();

            if (string.Equals(propName, "isSuccess", StringComparison.OrdinalIgnoreCase))
            {
                isSuccess = reader.GetBoolean();
            }
            else if (string.Equals(propName, "errors", StringComparison.OrdinalIgnoreCase))
            {
                errors = JsonSerializer.Deserialize(ref reader, ResultJsonSerializerContext.Default.ListError);
            }
            else
            {
                reader.Skip();
            }
        }

        throw new JsonException("Expected EndObject token.");
    }

    /// <summary>Writes a <see cref="Result"/> to JSON.</summary>
    /// <param name="writer">The UTF-8 JSON writer.</param>
    /// <param name="value">The result to serialize.</param>
    /// <param name="options">Serializer options.</param>
    public override void Write(Utf8JsonWriter writer, Result value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WriteBoolean("isSuccess", value.IsSuccess);
        if (value.IsFailure)
        {
            writer.WritePropertyName("errors");
            writer.WriteStartArray();
            var errors = value.Errors;
            for (int i = 0; i < errors.Count; i++)
            {
                JsonSerializer.Serialize(writer, errors[i], ResultJsonSerializerContext.Default.Error);
            }
            writer.WriteEndArray();
        }
        writer.WriteEndObject();
    }
}

/// <summary>JSON converter for <see cref="ErrorCollection"/> using System.Text.Json.</summary>
public sealed class ErrorCollectionJsonConverter : JsonConverter<ErrorCollection>
{
    /// <summary>Reads an <see cref="ErrorCollection"/> from a JSON array.</summary>
    /// <param name="reader">The UTF-8 JSON reader.</param>
    /// <param name="typeToConvert">The type to convert.</param>
    /// <param name="options">Serializer options.</param>
    /// <returns>The deserialized error collection.</returns>
    public override ErrorCollection Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var errors = JsonSerializer.Deserialize(ref reader, ResultJsonSerializerContext.Default.ErrorArray);
        return new ErrorCollection(errors);
    }

    /// <summary>Writes an <see cref="ErrorCollection"/> as a JSON array.</summary>
    /// <param name="writer">The UTF-8 JSON writer.</param>
    /// <param name="value">The error collection to serialize.</param>
    /// <param name="options">Serializer options.</param>
    public override void Write(Utf8JsonWriter writer, ErrorCollection value, JsonSerializerOptions options)
    {
        writer.WriteStartArray();
        for (int i = 0; i < value.Count; i++)
        {
            JsonSerializer.Serialize(writer, value[i], ResultJsonSerializerContext.Default.Error);
        }
        writer.WriteEndArray();
    }
}

/// <summary>JSON converter for <see cref="Result{TValue}"/> using reflection-based serialization.</summary>
/// <typeparam name="TValue">The value type contained in the result.</typeparam>
#pragma warning disable IL3050 // RequiresDynamicCode — generic converter inherently uses reflection for arbitrary TValue
[RequiresUnreferencedCode("ResultJsonConverter<TValue> uses reflection-based serialization for arbitrary TValue. For AOT, register a source-generated JsonSerializerContext for your specific Result<T>.")]
public sealed class ResultJsonConverter<TValue> : JsonConverter<Result<TValue>>
{
    /// <summary>Reads a <see cref="Result{TValue}"/> from JSON.</summary>
    /// <param name="reader">The UTF-8 JSON reader.</param>
    /// <param name="typeToConvert">The type to convert.</param>
    /// <param name="options">Serializer options.</param>
    /// <returns>The deserialized result.</returns>
    public override Result<TValue> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartObject)
        {
            throw new JsonException("Expected StartObject token.");
        }

        bool isSuccess = false;
        TValue? value = default;
        List<Error>? errors = null;
        bool hasValue = false;

        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject)
            {
                if (isSuccess && hasValue)
                    return Result<TValue>.Success(value!);
                return Result<TValue>.Failure(errors ?? [Error.Uninitialized]);
            }

            if (reader.TokenType != JsonTokenType.PropertyName)
            {
                throw new JsonException("Expected PropertyName token.");
            }

            string? propName = reader.GetString();
            reader.Read();

            if (string.Equals(propName, "isSuccess", StringComparison.OrdinalIgnoreCase))
            {
                isSuccess = reader.GetBoolean();
            }
            else if (string.Equals(propName, "value", StringComparison.OrdinalIgnoreCase))
            {
                value = JsonSerializer.Deserialize<TValue>(ref reader, options);
                hasValue = true;
            }
            else if (string.Equals(propName, "errors", StringComparison.OrdinalIgnoreCase))
            {
                errors = JsonSerializer.Deserialize(ref reader, ResultJsonSerializerContext.Default.ListError);
            }
            else
            {
                reader.Skip();
            }
        }

        throw new JsonException("Expected EndObject token.");
    }

    /// <summary>Writes a <see cref="Result{TValue}"/> to JSON.</summary>
    /// <param name="writer">The UTF-8 JSON writer.</param>
    /// <param name="value">The result to serialize.</param>
    /// <param name="options">Serializer options.</param>
    public override void Write(Utf8JsonWriter writer, Result<TValue> value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WriteBoolean("isSuccess", value.IsSuccess);
        if (value.IsSuccess)
        {
            writer.WritePropertyName("value");
            JsonSerializer.Serialize(writer, value.Value, options);
        }
        else
        {
            writer.WritePropertyName("errors");
            writer.WriteStartArray();
            var errors = value.Errors;
            for (int i = 0; i < errors.Count; i++)
            {
                JsonSerializer.Serialize(writer, errors[i], ResultJsonSerializerContext.Default.Error);
            }
            writer.WriteEndArray();
        }
        writer.WriteEndObject();
    }
}
#pragma warning restore IL3050

/// <summary>Source-generated JSON serializer context for Result types.</summary>
[JsonSerializable(typeof(Unit))]
[JsonSerializable(typeof(Error))]
[JsonSerializable(typeof(Error[]))]
[JsonSerializable(typeof(List<Error>))]
[JsonSerializable(typeof(ErrorType))]
[JsonSerializable(typeof(ErrorCollection))]
[JsonSerializable(typeof(Result))]
public partial class ResultJsonSerializerContext : JsonSerializerContext;
