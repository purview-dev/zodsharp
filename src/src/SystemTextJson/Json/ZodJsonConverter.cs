using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using ZodSharp.Core;

namespace ZodSharp.Json;

/// <summary>
/// System.Text.Json converter that validates using a Zod schema.
/// </summary>
/// <remarks>
/// <para>
/// Reading and writing go through a <see cref="JsonTypeInfo{T}"/> resolved from the serializer options
/// rather than the reflection-based <see cref="JsonSerializer"/> overloads. Those overloads are annotated
/// <c>RequiresUnreferencedCode</c>/<c>RequiresDynamicCode</c> whatever the options contain, so using them
/// here would make every consumer of this package unsafe under trimming and Native AOT. Resolving the type
/// info instead defers to whatever resolver the host configured: a source-generated
/// <see cref="JsonSerializerContext"/> in a trimmed or AOT application, or the default reflection resolver
/// elsewhere. The converter itself adds no reflection.
/// </para>
/// <para>
/// The options copy that excludes this converter is built once per converter instance. It used to be
/// rebuilt on every read and write, which is not just allocation: a fresh
/// <see cref="JsonSerializerOptions"/> has a cold metadata cache, so each call re-resolved the contract for
/// <typeparamref name="T"/>.
/// </para>
/// </remarks>
sealed class ZodJsonConverter<T>(IZodSchema<T, T> schema) : JsonConverter<T>
{
	JsonSerializerOptions? _withoutThisConverter;
	JsonTypeInfo<T>? _typeInfo;

	public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
	{
		// Buffer the current token so we can re-read it after extracting the value.
		using var document = JsonDocument.ParseValue(ref reader);
		T deserialized;
		try
		{
			deserialized =
				JsonSerializer.Deserialize(document.RootElement, TypeInfoFor(options))
				?? throw new JsonException("Failed to deserialize JSON");
		}
		catch (ZodJsonValidationException nested)
		{
			// A validating converter for a nested value was located within this element only. Its errors
			// already carry that relative path; a fresh exception lets the outer serializer add this value's.
			throw new ZodJsonValidationException(nested.Errors);
		}
		catch (JsonException unreadable)
		{
			// The inner serializer's path is relative to this element; the serializer would keep it as it is
			// and drop this value's own path. Record it, and let the serializer add this value's path.
			throw new ZodJsonReadException(ZodJsonReadException.RelativePathOf(unreadable), unreadable);
		}
		var result = schema.Validate(deserialized);

		// The serializer adds the converted value's path to the exception as it leaves the converter, so the
		// errors are reported under it (see ZodJsonValidationException.Errors).
		return result.IsSuccess ? result.Value : throw new ZodJsonValidationException(result.Errors);
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
			throw new ZodJsonValidationException(result.Errors);

		JsonSerializer.Serialize(writer, result.Value, TypeInfoFor(options));
	}

	/// <summary>
	/// Resolves the contract for <typeparamref name="T"/> from options that exclude this converter.
	/// </summary>
	/// <remarks>
	/// Cached per converter instance. A converter is registered against one options instance in practice, so
	/// the first resolution wins; a racing caller simply resolves the same contract again and the result is
	/// equivalent.
	/// </remarks>
	JsonTypeInfo<T> TypeInfoFor(JsonSerializerOptions options)
	{
		if (_typeInfo is { } cached)
			return cached;

		var effective = _withoutThisConverter ??= WithoutThisConverter(options);
		var resolved =
			// JsonSerializerOptions.GetTypeInfo<T>() returns JsonTypeInfo<T> directly, with no cast, but
			// it only exists from .NET 11. The older targets resolve through the non-generic overload.
#if NET11_0_OR_GREATER
			effective.GetTypeInfo<T>()
#else
			effective.GetTypeInfo(typeof(T)) as JsonTypeInfo<T>
#endif
			?? throw new JsonException(
				$"No JsonTypeInfo is available for '{typeof(T)}'. In a trimmed or Native AOT application, "
					+ "register the type with a source-generated JsonSerializerContext and set it as the "
					+ "options' TypeInfoResolver."
			);

		_typeInfo = resolved;

		return resolved;
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
