using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ZodSharp.Core;

namespace ZodSharp.Json;

/// <summary>
/// JsonConverter that validates using a Zod schema.
/// </summary>
sealed class ZodJsonConverter<T>(IZodSchema<T, T> schema) : JsonConverter<T>
{
	public override T ReadJson(
		JsonReader reader,
		Type objectType,
		T? existingValue,
		bool hasExistingValue,
		JsonSerializer serializer
	)
	{
		var token = JToken.Load(reader);
		var validationSerializer = CreateSerializerWithoutThisConverter(serializer);
		var deserialized =
			token.ToObject<T>(validationSerializer)
			?? throw new JsonSerializationException("Failed to deserialize JSON");

		var result = schema.Validate(deserialized);
		if (!result.IsSuccess)
		{
			var errorMessages = string.Join(", ", result.Errors.Select(e => e.Message));
			throw new JsonSerializationException($"Validation failed: {errorMessages}");
		}

		return result.Value!;
	}

	public override void WriteJson(JsonWriter writer, T? value, JsonSerializer serializer)
	{
		if (value == null)
		{
			writer.WriteNull();
			return;
		}

		var result = schema.Validate(value);
		if (!result.IsSuccess)
		{
			var errorMessages = string.Join(", ", result.Errors.Select(e => e.Message));
			throw new JsonSerializationException($"Validation failed: {errorMessages}");
		}

		var token = JToken.FromObject(value, CreateSerializerWithoutThisConverter(serializer));
		token.WriteTo(writer);
	}

	JsonSerializer CreateSerializerWithoutThisConverter(JsonSerializer serializer)
	{
		JsonSerializer clone = new()
		{
			CheckAdditionalContent = serializer.CheckAdditionalContent,
			ConstructorHandling = serializer.ConstructorHandling,
			Context = serializer.Context,
			ContractResolver = serializer.ContractResolver,
			Culture = serializer.Culture,
			DateFormatHandling = serializer.DateFormatHandling,
			DateFormatString = serializer.DateFormatString,
			DateParseHandling = serializer.DateParseHandling,
			DateTimeZoneHandling = serializer.DateTimeZoneHandling,
			DefaultValueHandling = serializer.DefaultValueHandling,
			EqualityComparer = serializer.EqualityComparer,
			FloatFormatHandling = serializer.FloatFormatHandling,
			FloatParseHandling = serializer.FloatParseHandling,
			Formatting = serializer.Formatting,
			MaxDepth = serializer.MaxDepth,
			MetadataPropertyHandling = serializer.MetadataPropertyHandling,
			MissingMemberHandling = serializer.MissingMemberHandling,
			NullValueHandling = serializer.NullValueHandling,
			ObjectCreationHandling = serializer.ObjectCreationHandling,
			PreserveReferencesHandling = serializer.PreserveReferencesHandling,
			ReferenceLoopHandling = serializer.ReferenceLoopHandling,
			SerializationBinder = serializer.SerializationBinder,
			StringEscapeHandling = serializer.StringEscapeHandling,
			TraceWriter = serializer.TraceWriter,
			TypeNameAssemblyFormatHandling = serializer.TypeNameAssemblyFormatHandling,
			TypeNameHandling = serializer.TypeNameHandling,
		};

		foreach (var converter in serializer.Converters.Where(converter => !ReferenceEquals(converter, this)))
		{
			clone.Converters.Add(converter);
		}

		return clone;
	}
}
