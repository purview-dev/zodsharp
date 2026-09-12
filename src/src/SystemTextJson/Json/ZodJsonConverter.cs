using System.Text.Json;
using System.Text.Json.Serialization;
using ZodSharp.Core;

namespace ZodSharp.Json;

/// <summary>
/// System.Text.Json converter that validates using a Zod schema.
/// </summary>
sealed class ZodJsonConverter<T>(IZodSchema<T, T> schema) : JsonConverter<T>
{
	public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
	{
		// Buffer the current token so we can re-read it after extracting the value.
		using var document = JsonDocument.ParseValue(ref reader);
		var deserialized =
			document.Deserialize<T>(WithoutThisConverter(options))
			?? throw new JsonException("Failed to deserialize JSON");
		var result = schema.Validate(deserialized);
		if (!result.IsSuccess)
		{
			var errorMessages = string.Join(", ", result.Errors.Select(e => e.Message));
			throw new JsonException($"Validation failed: {errorMessages}");
		}

		return result.Value;
	}

	public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
	{
		if (value == null)
		{
			writer.WriteNullValue();
			return;
		}

		var result = schema.Validate(value);
		if (!result.IsSuccess)
		{
			var errorMessages = string.Join(", ", result.Errors.Select(e => e.Message));
			throw new JsonException($"Validation failed: {errorMessages}");
		}

		JsonSerializer.Serialize(writer, result.Value, WithoutThisConverter(options));
	}

	/// <summary>
	/// Creates a shallow copy of <paramref name="options"/> with this converter removed,
	/// preventing infinite recursion during (de)serialization.
	/// </summary>
	JsonSerializerOptions WithoutThisConverter(JsonSerializerOptions options)
	{
		JsonSerializerOptions copy = new(options);

		for (var i = copy.Converters.Count - 1; i >= 0; i--)
		{
			if (ReferenceEquals(copy.Converters[i], this))
			{
				copy.Converters.RemoveAt(i);
			}
		}

		return copy;
	}
}
