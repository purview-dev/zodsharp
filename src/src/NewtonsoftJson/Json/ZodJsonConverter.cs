using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ZodSharp.Core;

namespace ZodSharp.Json;

/// <summary>
/// JsonConverter that validates using a Zod schema.
/// </summary>
[System.Diagnostics.CodeAnalysis.RequiresUnreferencedCode(NewtonsoftJsonTrimming.Message)]
[System.Diagnostics.CodeAnalysis.RequiresDynamicCode(NewtonsoftJsonTrimming.Message)]
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
		// Capture the location before the value is consumed, so validation errors are reported under it.
		var path = reader.Path;
		var (line, position) = LineInfo(reader as IJsonLineInfo);

		var token = JToken.Load(reader);
		var validationSerializer = CreateSerializerWithoutThisConverter(serializer);

		T deserialized;
		try
		{
			deserialized =
				token.ToObject<T>(validationSerializer)
				?? throw new JsonSerializationException("Failed to deserialize JSON", path, line, position, null);
		}
		catch (ZodNewtonsoftJsonValidationException nested)
		{
			// A validating converter for a nested value only knows its path within this token; locate it
			// under this value's path too.
			throw new ZodNewtonsoftJsonValidationException(nested.Errors, path, line, position);
		}

		var result = schema.Validate(deserialized);

		return result.IsSuccess
			? result.Value
			: throw new ZodNewtonsoftJsonValidationException(result.Errors, path, line, position);
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
			throw new ZodNewtonsoftJsonValidationException(result.Errors, writer.Path, 0, 0);

		var token = JToken.FromObject(value, CreateSerializerWithoutThisConverter(serializer));
		token.WriteTo(writer);
	}

	static (int Line, int Position) LineInfo(IJsonLineInfo? info) =>
		info is { } lineInfo && lineInfo.HasLineInfo() ? (lineInfo.LineNumber, lineInfo.LinePosition) : (0, 0);

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
