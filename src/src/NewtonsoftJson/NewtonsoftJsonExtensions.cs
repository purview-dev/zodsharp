using System.Collections.Immutable;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ZodSharp.Core;
using ZodSharp.Json;

namespace ZodSharp;

/// <summary>
/// Extensions for integrating ZodSharp with Newtonsoft.Json.
/// </summary>
#if !NETSTANDARD2_1_OR_GREATER
[System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
#endif
public static class NewtonsoftJsonExtensions
{
	/// <summary>
	/// The code of every failure to read JSON: malformed JSON, a value that cannot be converted to the target
	/// type, or a <see langword="null"/> document. The error's <see cref="ValidationError.Category"/> is
	/// <see cref="ZodIssueCategories.InvalidType"/>, its <see cref="ValidationError.FullPath"/> is the location
	/// the serializer reported, and its <c>line</c> and <c>position</c> parameters hold the serializer's
	/// (one-based) line number and line position when it has them.
	/// </summary>
	public const string InvalidJsonCode = "invalid_json";

	/// <summary>
	/// Deserializes JSON and validates it using a Zod schema.
	/// </summary>
	public static ValidationResult<T> DeserializeAndValidate<T>(
		this IZodSchema<T, T> schema,
		string json,
		JsonSerializerSettings? settings = null
	)
	{
		if (schema == null)
			throw new ArgumentNullException(nameof(schema));

		return TryDeserialize<T>(json, settings, out var deserialized, out var errors)
			? schema.Validate(deserialized)
			: ValidationResult<T>.Failure(errors);
	}

	/// <summary>
	/// Deserializes JSON and validates it using a Zod schema (async).
	/// </summary>
	public static async Task<ValidationResult<T>> DeserializeAndValidateAsync<T>(
		this IZodSchema<T, T> schema,
		Stream jsonStream,
		JsonSerializerSettings? settings = null,
		CancellationToken cancellationToken = default
	)
	{
		if (schema == null)
			throw new ArgumentNullException(nameof(schema));
		if (jsonStream == null)
			throw new ArgumentNullException(nameof(jsonStream));

#if NETSTANDARD2_1_OR_GREATER
		using StreamReader reader = new(jsonStream, Encoding.UTF8, true, 1024, true);
		var json = await reader.ReadToEndAsync().ConfigureAwait(false);
#else
		using StreamReader reader = new(jsonStream, Encoding.UTF8, leaveOpen: true);
		var json = await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
#endif

		// Validate asynchronously, so a schema's async refinements run rather than being skipped.
		return TryDeserialize<T>(json, settings, out var deserialized, out var errors)
			? await schema.ValidateAsync(deserialized, cancellationToken).ConfigureAwait(false)
			: ValidationResult<T>.Failure(errors);
	}

	/// <summary>
	/// Deserializes JSON from a JToken and validates it using a Zod schema.
	/// </summary>
	public static ValidationResult<T> DeserializeAndValidate<T>(
		this IZodSchema<T, T> schema,
		JToken token,
		JsonSerializer? serializer = null
	)
	{
		if (schema == null)
			throw new ArgumentNullException(nameof(schema));
		if (token == null)
			throw new ArgumentNullException(nameof(token));

		serializer ??= JsonSerializer.CreateDefault();

		T? deserialized;
		try
		{
			deserialized = token.ToObject<T>(serializer);
		}
		catch (Exception ex)
			when (JsonDeserializationErrors.TryMap(ex, serializer.ContractResolver, typeof(T), out var errors))
		{
			return ValidationResult<T>.Failure(errors);
		}

		return deserialized == null
			? ValidationResult<T>.Failure(JsonDeserializationErrors.NullValue(serializer.ContractResolver, typeof(T)))
			: schema.Validate(deserialized);
	}

	static bool TryDeserialize<T>(
		string json,
		JsonSerializerSettings? settings,
		[System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out T? deserialized,
		out ImmutableArray<ValidationError> errors
	)
	{
		var serializer = JsonSerializer.CreateDefault(settings);
		try
		{
			deserialized = JsonConvert.DeserializeObject<T>(json, settings);
		}
		catch (Exception ex)
			when (JsonDeserializationErrors.TryMap(ex, serializer.ContractResolver, typeof(T), out var mapped))
		{
			deserialized = default;
			errors = mapped;
			return false;
		}

		if (deserialized == null)
		{
			errors = [JsonDeserializationErrors.NullValue(serializer.ContractResolver, typeof(T))];
			return false;
		}

		errors = [];
		return true;
	}

	/// <summary>
	/// Creates a custom JsonConverter that validates using a Zod schema.
	/// </summary>
	public static JsonConverter CreateValidatingConverter<T>(this IZodSchema<T, T> schema) =>
		schema == null ? throw new ArgumentNullException(nameof(schema)) : new ZodJsonConverter<T>(schema);

	/// <summary>
	/// Validates a value and serializes it to a JSON string.
	/// </summary>
	public static ValidationResult<string> ValidateAndSerialize<T>(
		this IZodSchema<T, T> schema,
		T value,
		JsonSerializerSettings? settings = null,
		Formatting formatting = Formatting.None
	)
	{
		if (schema == null)
			throw new ArgumentNullException(nameof(schema));

		var result = schema.Validate(value);
		if (!result.IsSuccess)
			return ValidationResult<string>.Failure(result.Errors);

		var json = JsonConvert.SerializeObject(result.Value, formatting, settings);
		return ValidationResult<string>.Success(json);
	}

	/// <summary>
	/// Validates a value and serializes it to a stream (async).
	/// </summary>
	public static async Task<ValidationResult<string>> ValidateAndSerializeAsync<T>(
		this IZodSchema<T, T> schema,
		T value,
		Stream output,
		JsonSerializerSettings? settings = null,
		Formatting formatting = Formatting.Indented,
		CancellationToken cancellationToken = default
	)
	{
		if (schema == null)
			throw new ArgumentNullException(nameof(schema));
		if (output == null)
			throw new ArgumentNullException(nameof(output));

		var result = await schema.ValidateAsync(value, cancellationToken);
		if (!result.IsSuccess)
			return ValidationResult<string>.Failure(result.Errors);

		var serializer = JsonSerializer.CreateDefault(settings);
		serializer.Formatting = formatting;
#if NETSTANDARD2_1_OR_GREATER
		using StreamWriter writer = new(output, new UTF8Encoding(false), 1024, leaveOpen: true);
#else
		await using StreamWriter writer = new(output, new UTF8Encoding(false), 1024, leaveOpen: true);
#endif
		using JsonWriter jsonWriter = new JsonTextWriter(writer) { CloseOutput = false };
		serializer.Serialize(jsonWriter, result.Value);
		await jsonWriter.FlushAsync(cancellationToken);

		return ValidationResult<string>.Success(string.Empty);
	}
}
