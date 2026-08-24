using System.Text.Json;
using System.Text.Json.Serialization;

namespace Enterprise.Patterns.Result;

public sealed class ResultJsonConverter : JsonConverter<Result>
{
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

    public override void Write(Utf8JsonWriter writer, Result value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WriteBoolean("isSuccess", value.IsSuccess);
        if (value.IsFailure)
        {
            writer.WritePropertyName("errors");
            JsonSerializer.Serialize(writer, value.Errors.ToArray(), ResultJsonSerializerContext.Default.ErrorArray);
        }
        writer.WriteEndObject();
    }
}

public sealed class ErrorCollectionJsonConverter : JsonConverter<ErrorCollection>
{
    public override ErrorCollection Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var errors = JsonSerializer.Deserialize(ref reader, ResultJsonSerializerContext.Default.ErrorArray);
        return new ErrorCollection(errors);
    }

    public override void Write(Utf8JsonWriter writer, ErrorCollection value, JsonSerializerOptions options)
    {
        JsonSerializer.Serialize(writer, value.ToArray(), ResultJsonSerializerContext.Default.ErrorArray);
    }
}

[JsonSerializable(typeof(Unit))]
[JsonSerializable(typeof(Error))]
[JsonSerializable(typeof(Error[]))]
[JsonSerializable(typeof(List<Error>))]
[JsonSerializable(typeof(ErrorType))]
[JsonSerializable(typeof(ErrorCollection))]
[JsonSerializable(typeof(Result))]
public partial class ResultJsonSerializerContext : JsonSerializerContext;
