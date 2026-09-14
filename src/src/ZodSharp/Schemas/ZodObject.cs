using System.Collections.Immutable;
using ZodSharp.Core;

namespace ZodSharp.Schemas;

/// <summary>
/// Schema for object validation.
/// Validates object properties against their schemas.
/// </summary>
/// <remarks>
/// Initializes a new instance of the ZodObject class.
/// </remarks>
/// <param name="shape">The object shape (property schemas)</param>
/// <param name="unknownKeyPolicy">How unknown keys are handled.</param>
/// <param name="optionalKeys">Optional field names.</param>
/// <param name="catchallSchema">Schema for unknown keys.</param>
/// <param name="requiredKeys">Field names whose presence is required, overriding schema-level optionality.</param>
public class ZodObject(
	ImmutableDictionary<string, IZodSchema<object, object>> shape,
	UnknownKeyPolicy unknownKeyPolicy = UnknownKeyPolicy.Strip,
	ImmutableHashSet<string>? optionalKeys = null,
	IZodSchema<object, object>? catchallSchema = null,
	ImmutableHashSet<string>? requiredKeys = null
) : ZodType<Dictionary<string, object?>, Dictionary<string, object?>>
{
	static readonly string[] EmptyPath = [];

	/// <summary>
	/// The object shape: a map of field names to their schemas.
	/// </summary>
	public ImmutableDictionary<string, IZodSchema<object, object>> Shape => shape;

	/// <summary>
	/// How unknown keys (not in <see cref="Shape"/>) are handled.
	/// </summary>
	public UnknownKeyPolicy UnknownKeyPolicy => unknownKeyPolicy;

	/// <summary>
	/// The set of field names that are optional (missing fields are allowed).
	/// </summary>
	public ImmutableHashSet<string> OptionalKeys => optionalKeys ?? [];

	/// <summary>
	/// The set of field names whose presence is explicitly required, even when
	/// the field schema otherwise accepts a missing value.
	/// </summary>
	public ImmutableHashSet<string> RequiredKeys => requiredKeys ?? [];

	/// <summary>
	/// The catchall schema for unknown keys, or <see langword="null"/> when not set.
	/// </summary>
	public IZodSchema<object, object>? CatchallSchema => catchallSchema;

	/// <summary>
	/// Parses and validates an object value.
	/// </summary>
	/// <param name="value">The value to validate</param>
	/// <returns>A validation result</returns>
	protected override ValidationResult<Dictionary<string, object?>> ParseInternal(Dictionary<string, object?> value)
	{
		if (value == null)
		{
			return ValidationResult<Dictionary<string, object?>>.Failure(
				new ValidationError("invalid_type", "Expected object, but got null", EmptyPath)
			);
		}

		List<ValidationError>? errors = null;
		Dictionary<string, object?>? rebuilt = null;

		foreach (var (key, schema) in shape)
			ValidateField(value, key, schema, ref errors, ref rebuilt);

		foreach (var (key, propertyValue) in value)
			ValidateUnknownKey(value, key, propertyValue, ref errors, ref rebuilt);

		if (errors is { Count: > 0 })
			return ValidationResult<Dictionary<string, object?>>.Failure(errors);

		// When every field passed through unchanged and nothing needed stripping or
		// injecting, the input dictionary is already the validated result.
		return rebuilt is not null
			? ValidationResult<Dictionary<string, object?>>.Success(rebuilt)
			: ValidationResult<Dictionary<string, object?>>.Success(value);
	}

	/// <summary>
	/// Validates a single field from the shape against the input value, recording any
	/// error or, when the validated value differs (transform/default), triggering a
	/// rebuild of the output object.
	/// </summary>
	void ValidateField(
		Dictionary<string, object?> value,
		string key,
		IZodSchema<object, object> schema,
		ref List<ValidationError>? errors,
		ref Dictionary<string, object?>? rebuilt
	)
	{
		if (!value.TryGetValue(key, out var propertyValue))
		{
			var allowsMissing =
				!IsRequiredKey(key) && (IsOptionalKey(key) || schema is IOptionalSchema { IsOptional: true });

			if (!allowsMissing)
			{
				errors ??= [];
				errors.Add(new ValidationError("missing_field", $"Required field '{key}' is missing", [key]));
				return;
			}

			if (schema is IOptionalSchema { ProvidesValueOnMissing: true })
			{
				var missingResult = schema.Validate(null!);
				if (missingResult.IsSuccess && missingResult.Value is not null)
					GetRebuilt(value, ref rebuilt)[key] = missingResult.Value;
			}

			return;
		}

		var result = schema.Validate(propertyValue!);

		if (!result.IsSuccess)
		{
			errors ??= [];
			foreach (var error in result.Errors)
			{
				var path = new string[error.Path.Length + 1];
				path[0] = key;

				error.Path.CopyTo(0, path, 1, error.Path.Length);
				errors.Add(new(error.Code, error.Message, path, error.Parameters));
			}

			return;
		}

		if (!Equals(result.Value, propertyValue))
			GetRebuilt(value, ref rebuilt)[key] = result.Value;
	}

	/// <summary>
	/// Handles a key that is not part of the shape according to the unknown-key policy.
	/// </summary>
	void ValidateUnknownKey(
		Dictionary<string, object?> value,
		string key,
		object? propertyValue,
		ref List<ValidationError>? errors,
		ref Dictionary<string, object?>? rebuilt
	)
	{
		if (shape.ContainsKey(key))
			return;

		if (catchallSchema is not null)
		{
			var catchallResult = catchallSchema.Validate(propertyValue!);
			if (!catchallResult.IsSuccess)
			{
				errors ??= [];
				foreach (var error in catchallResult.Errors)
				{
					var path = new string[error.Path.Length + 1];
					path[0] = key;
					error.Path.CopyTo(0, path, 1, error.Path.Length);
					errors.Add(new(error.Code, error.Message, path, error.Parameters));
				}
			}
			else
			{
				GetRebuilt(value, ref rebuilt)[key] = catchallResult.Value;
			}

			return;
		}

#pragma warning disable IDE0010 // Add missing cases
		switch (unknownKeyPolicy)
		{
			case UnknownKeyPolicy.Passthrough:
				break;
			case UnknownKeyPolicy.Strict:
				errors ??= [];
				errors.Add(new ValidationError("unrecognized_key", $"Unrecognized key '{key}'", [key]));
				break;
			default:
				// Strip: drop the unknown key by forcing the rebuilt copy.
				GetRebuilt(value, ref rebuilt);
				break;
		}
#pragma warning restore IDE0010 // Add missing cases
	}

	/// <summary>
	/// Creates (once) a copy of the input containing only the shape keys. Unknown keys
	/// are dropped as a side effect, which is exactly what strip semantics need.
	/// </summary>
	Dictionary<string, object?> GetRebuilt(Dictionary<string, object?> value, ref Dictionary<string, object?>? rebuilt)
	{
		if (rebuilt is null)
		{
			rebuilt = [with(shape.Count)];
			foreach (var (key, propertyValue) in value)
			{
				if (shape.ContainsKey(key))
					rebuilt[key] = propertyValue;
			}
		}

		return rebuilt;
	}

	/// <summary>
	/// Returns <see langword="true"/> if <paramref name="key"/> is optional.
	/// </summary>
	bool IsOptionalKey(string key) => optionalKeys is not null && optionalKeys.Contains(key);

	/// <summary>
	/// Returns <see langword="true"/> if <paramref name="key"/>'s presence is explicitly required.
	/// </summary>
	bool IsRequiredKey(string key) => requiredKeys is not null && requiredKeys.Contains(key);

	/// <summary>
	/// Creates a new <see cref="ZodObject"/> with an additional or replaced field.
	/// Equivalent to Zod's <c>.extend({ key: schema })</c>. Replacing an existing key
	/// drops its previous optional-key metadata, so the field's optionality is governed
	/// solely by the new <paramref name="fieldSchema"/>.
	/// </summary>
	/// <typeparam name="T">The field type.</typeparam>
	/// <param name="key">The field name.</param>
	/// <param name="fieldSchema">The field schema.</param>
	/// <returns>A new <see cref="ZodObject"/> with the extended shape.</returns>
	public ZodObject Extend<T>(string key, IZodSchema<T, T> fieldSchema)
	{
		if (string.IsNullOrWhiteSpace(key))
			throw new ArgumentException("Key must not be null or whitespace.", nameof(key));
		if (fieldSchema is null)
			throw new ArgumentNullException(nameof(fieldSchema));

		var newShape = shape.SetItem(key, FieldSchemaWrapper<T>.Wrap(fieldSchema));
		var newOptionalKeys = optionalKeys?.Remove(key);
		var newRequiredKeys = requiredKeys?.Remove(key);
		return new ZodObject(newShape, unknownKeyPolicy, newOptionalKeys, catchallSchema, newRequiredKeys);
	}

	/// <summary>
	/// Creates a new <see cref="ZodObject"/> by merging another object's shape into this one.
	/// Equivalent to Zod's <c>.merge(other)</c>. Keys in <paramref name="other"/> override
	/// keys in this object, and the merged object adopts <paramref name="other"/>'s unknown-key
	/// policy and catchall schema. Optionality follows the contributing object for each key.
	/// </summary>
	/// <param name="other">The object to merge.</param>
	/// <returns>A new merged <see cref="ZodObject"/>.</returns>
	public ZodObject Merge(ZodObject other)
	{
		if (other is null)
			throw new ArgumentNullException(nameof(other));

		var newShape = shape;
		foreach (var (key, schema) in other.Shape)
			newShape = newShape.SetItem(key, schema);

		var mergedOptionalKeys = (optionalKeys ?? []).Except(other.Shape.Keys).Union(other.OptionalKeys);
		var mergedRequiredKeys = (requiredKeys ?? []).Except(other.Shape.Keys).Union(other.RequiredKeys);

		return new ZodObject(
			newShape,
			other.UnknownKeyPolicy,
			mergedOptionalKeys,
			other.CatchallSchema,
			mergedRequiredKeys
		);
	}

	/// <summary>
	/// Creates a new <see cref="ZodObject"/> containing only the specified keys.
	/// Equivalent to Zod's <c>.pick({ keys })</c>.
	/// </summary>
	/// <param name="keys">The keys to keep.</param>
	/// <returns>A new <see cref="ZodObject"/> with only the picked keys.</returns>
	public ZodObject Pick(params string[] keys)
	{
		if (keys is null || keys.Length == 0)
			throw new ArgumentException("Must specify at least one key to pick.", nameof(keys));

		var keySet = keys.ToHashSet();
		var newShape = shape.Where(kv => keySet.Contains(kv.Key)).ToImmutableDictionary();
		var newOptional = optionalKeys?.Where(keySet.Contains).ToImmutableHashSet();
		var newRequired = requiredKeys?.Where(keySet.Contains).ToImmutableHashSet();
		return new ZodObject(newShape, unknownKeyPolicy, newOptional, catchallSchema, newRequired);
	}

	/// <summary>
	/// Creates a new <see cref="ZodObject"/> excluding the specified keys.
	/// Equivalent to Zod's <c>.omit({ keys })</c>.
	/// </summary>
	/// <param name="keys">The keys to remove.</param>
	/// <returns>A new <see cref="ZodObject"/> without the omitted keys.</returns>
	public ZodObject Omit(params string[] keys)
	{
		if (keys is null || keys.Length == 0)
			throw new ArgumentException("Must specify at least one key to omit.", nameof(keys));

		var keySet = keys.ToHashSet();
		var newShape = shape.Where(kv => !keySet.Contains(kv.Key)).ToImmutableDictionary();
		var newOptional = optionalKeys?.Where(k => !keySet.Contains(k)).ToImmutableHashSet();
		var newRequired = requiredKeys?.Where(k => !keySet.Contains(k)).ToImmutableHashSet();
		return new ZodObject(newShape, unknownKeyPolicy, newOptional, catchallSchema, newRequired);
	}

	/// <summary>
	/// Creates a new <see cref="ZodObject"/> where all fields are optional (missing fields
	/// are allowed). Equivalent to Zod's <c>.partial()</c>.
	/// </summary>
	/// <returns>A new <see cref="ZodObject"/> with all keys optional.</returns>
	public ZodObject Partial() => new(shape, unknownKeyPolicy, [.. shape.Keys], catchallSchema, null);

	/// <summary>
	/// Creates a new <see cref="ZodObject"/> where all fields are required (no optional keys).
	/// Equivalent to Zod's <c>.required()</c>.
	/// </summary>
	/// <returns>A new <see cref="ZodObject"/> with no optional keys.</returns>
	public ZodObject Required() => new(shape, unknownKeyPolicy, null, catchallSchema, [.. shape.Keys]);

	/// <summary>
	/// Creates a new <see cref="ZodObject"/> that keeps unknown keys in the output.
	/// Equivalent to Zod's <c>.passthrough()</c>.
	/// </summary>
	public ZodObject Passthrough() =>
		new(shape, UnknownKeyPolicy.Passthrough, optionalKeys, catchallSchema, requiredKeys);

	/// <summary>
	/// Creates a new <see cref="ZodObject"/> that rejects unknown keys with an error.
	/// Equivalent to Zod's <c>.strict()</c>.
	/// </summary>
	public ZodObject Strict() => new(shape, UnknownKeyPolicy.Strict, optionalKeys, catchallSchema, requiredKeys);

	/// <summary>
	/// Creates a new <see cref="ZodObject"/> that silently drops unknown keys.
	/// Equivalent to Zod's <c>.strip()</c> (the default).
	/// </summary>
	public ZodObject Strip() => new(shape, UnknownKeyPolicy.Strip, optionalKeys, catchallSchema, requiredKeys);

	/// <summary>
	/// Creates a new <see cref="ZodObject"/> that validates unknown keys against
	/// <paramref name="schema"/> and includes them in the output. Equivalent to Zod's
	/// <c>.catchall(schema)</c>.
	/// </summary>
	/// <param name="schema">The catchall schema for unknown keys.</param>
	public ZodObject Catchall(IZodSchema<object, object> schema)
	{
		return schema is null
			? throw new ArgumentNullException(nameof(schema))
			: new ZodObject(shape, unknownKeyPolicy, optionalKeys, schema, requiredKeys);
	}
}
