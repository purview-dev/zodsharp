using System.Globalization;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ZodSharp.Core;
using ZodSharp.Schemas;

namespace ZodSharp.JsonSchema;

/// <summary>
/// Options for parsing JSON Schema to ZodSharp schemas.
/// </summary>
public class FromJsonSchemaOptions
{
	// Reserved for future options
}

/// <summary>
/// Parses JSON Schema (Draft 2020-12) to ZodSharp schemas.
/// Following the same patterns as Zod's fromJSONSchema.
/// </summary>
public static class FromJsonSchemaParser
{
	/// <summary>
	/// Parses a JSON Schema definition to a ZodSharp schema.
	/// </summary>
	/// <param name="schema">The JSON Schema definition</param>
	/// <param name="options">Parsing options</param>
	/// <returns>A ZodSharp schema that validates according to the JSON Schema</returns>
	public static IZodSchema<object, object> Parse(JsonSchemaDefinition schema,
#pragma warning disable IDE0060 // Remove unused parameter
		FromJsonSchemaOptions? options = null
#pragma warning restore IDE0060 // Remove unused parameter
	)
	{
		if (schema is null)
		{
			throw new ArgumentNullException(nameof(schema));
		}

		ConversionContext ctx = new(schema, schema.Defs ?? schema.Definitions ?? []);

		return ConvertSchema(schema, ctx);
	}

	/// <summary>
	/// Parses a JSON Schema string to a ZodSharp schema.
	/// </summary>
	/// <param name="jsonSchema">The JSON Schema as a string</param>
	/// <param name="options">Parsing options</param>
	/// <returns>A ZodSharp schema that validates according to the JSON Schema</returns>
	public static IZodSchema<object, object> Parse(string jsonSchema, FromJsonSchemaOptions? options = null)
	{
		var schema = JsonConvert.DeserializeObject<JsonSchemaDefinition>(
			jsonSchema,
			JsonSchemaSerializerOptions.Reading
		);

		return schema == null
			? throw new ArgumentException("Invalid JSON Schema: could not parse JSON", nameof(jsonSchema))
			: Parse(schema, options);
	}

	sealed class ConversionContext(JsonSchemaDefinition rootSchema, Dictionary<string, JsonSchemaDefinition> defs)
	{
		public JsonSchemaDefinition RootSchema { get; } = rootSchema;

		public Dictionary<string, JsonSchemaDefinition> Defs { get; } = defs;

		public Dictionary<string, IZodSchema<object, object>> Refs { get; } = [];

		public HashSet<string> Processing { get; } = [];
	}

	static IZodSchema<object, object> ConvertSchema(JsonSchemaDefinition schema, ConversionContext ctx)
	{
		// Handle $ref
		if (schema.Ref != null)
		{
			return ResolveRef(schema.Ref, ctx);
		}

		// Handle enum
		if (schema.Enum != null && schema.Enum.Count > 0)
		{
			if (schema.Enum.Count == 1)
			{
				return CreateLiteralSchema(schema.Enum[0]);
			}

			// Create union of literals
			var literals = schema.Enum.Select(CreateLiteralSchema).ToArray();
			return new ZodUnion(literals);
		}

		// Handle const
		if (schema.Const != null)
		{
			return CreateLiteralSchema(schema.Const);
		}

		// Handle composition keywords
		if (schema.AnyOf != null && schema.AnyOf.Count > 0)
		{
			var options = schema.AnyOf.Select(s => ConvertSchema(s, ctx)).ToArray();
			return new ZodUnion(options);
		}

		if (schema.OneOf != null && schema.OneOf.Count > 0)
		{
			// oneOf is semantically different (exactly one must match) but we approximate with union
			var options = schema.OneOf.Select(s => ConvertSchema(s, ctx)).ToArray();
			return new ZodUnion(options);
		}

		if (schema.AllOf != null && schema.AllOf.Count > 0)
		{
			// allOf requires all to match - we can only fully support this for objects
			// For now, just use the first schema
			return ConvertSchema(schema.AllOf[0], ctx);
		}

		// Handle type
		if (schema.Type == null)
		{
			// No type specified - any
			return new AnySchema();
		}

		var result = schema.Type switch
		{
			"string" => ConvertStringSchema(schema),
			"number" => ConvertNumberSchema(schema, isInteger: false),
			"integer" => ConvertNumberSchema(schema, isInteger: true),
			"boolean" => new BooleanSchemaWrapper(),
			"null" => new NullSchemaWrapper(),
			"object" => ConvertObjectSchema(schema, ctx),
			"array" => ConvertArraySchema(schema, ctx),
			_ => new AnySchema(),
		};

		return result;
	}

	[System.Diagnostics.CodeAnalysis.SuppressMessage(
		"Performance",
		"CA1859:Use concrete types when possible for improved performance"
	)]
	static IZodSchema<object, object> ConvertStringSchema(JsonSchemaDefinition schema)
	{
		var stringSchema = Z.String();

		// Apply constraints
		if (schema.MinLength.HasValue)
		{
			stringSchema = stringSchema.Min(schema.MinLength.Value);
		}

		if (schema.MaxLength.HasValue)
		{
			stringSchema = stringSchema.Max(schema.MaxLength.Value);
		}

		if (schema.Pattern != null)
		{
			stringSchema = stringSchema.Regex(new Regex(schema.Pattern));
		}

		// Apply format
		if (schema.Format != null)
		{
			stringSchema = schema.Format switch
			{
				"email" => stringSchema.Email(),
				"uri" or "uri-reference" => stringSchema.Url(),
				"uuid" or "guid" => stringSchema.UUID(),
				_ => stringSchema, // Ignore unknown formats
			};
		}

		return new StringSchemaWrapper(stringSchema);
	}

	[System.Diagnostics.CodeAnalysis.SuppressMessage(
		"Performance",
		"CA1859:Use concrete types when possible for improved performance"
	)]
	static IZodSchema<object, object> ConvertNumberSchema(JsonSchemaDefinition schema, bool isInteger)
	{
		var numberSchema = Z.Number();

		if (isInteger)
		{
			numberSchema = numberSchema.Int();
		}

		// Apply constraints
		if (schema.Minimum.HasValue)
		{
			numberSchema = numberSchema.Min(schema.Minimum.Value);
		}

		if (schema.Maximum.HasValue)
		{
			numberSchema = numberSchema.Max(schema.Maximum.Value);
		}

		if (schema.MultipleOf.HasValue)
		{
			numberSchema = numberSchema.MultipleOf(schema.MultipleOf.Value);
		}

		return new NumberSchemaWrapper(numberSchema);
	}

	[System.Diagnostics.CodeAnalysis.SuppressMessage(
		"Performance",
		"CA1859:Use concrete types when possible for improved performance"
	)]
	static IZodSchema<object, object> ConvertObjectSchema(JsonSchemaDefinition schema, ConversionContext ctx)
	{
		var builder = Z.Object();
		HashSet<string> requiredSet = [.. schema.Required ?? []];

		if (schema.Properties != null)
		{
			foreach (var (key, propSchema) in schema.Properties)
			{
				var propZodSchema = ConvertSchema(propSchema, ctx);

				if (requiredSet.Contains(key))
				{
					builder = builder.Field(key, propZodSchema);
				}
				else
				{
					// Make optional - wrap in optional
					builder = builder.Field(key, new OptionalSchemaWrapper(propZodSchema));
				}
			}
		}

		return new ObjectSchemaWrapper(builder.Build());
	}

	[System.Diagnostics.CodeAnalysis.SuppressMessage(
		"Performance",
		"CA1859:Use concrete types when possible for improved performance"
	)]
	static IZodSchema<object, object> ConvertArraySchema(JsonSchemaDefinition schema, ConversionContext ctx)
	{
		var elementSchema = schema.Items != null ? ConvertSchema(schema.Items, ctx) : new AnySchema();

		return new ArraySchemaWrapper(elementSchema, schema.MinItems, schema.MaxItems);
	}

	static IZodSchema<object, object> ResolveRef(string refPath, ConversionContext ctx)
	{
		if (!refPath.StartsWith('#'))
		{
			throw new NotSupportedException("External $ref is not supported, only local refs (#/...) are allowed");
		}

		// Check if already resolved
		if (ctx.Refs.TryGetValue(refPath, out var existing))
		{
			return existing;
		}

		// Check for circular reference
		if (ctx.Processing.Contains(refPath))
		{
			// Return a lazy placeholder
			return new LazySchemaWrapper(() =>
				ctx.Refs.TryGetValue(refPath, out var resolved) ? resolved : new AnySchema()
			);
		}

		ctx.Processing.Add(refPath);

		// Parse the path
		var parts = refPath[1..].Split('/').Where(p => !string.IsNullOrEmpty(p)).ToArray();

		JsonSchemaDefinition? resolved = null;

		if (parts.Length == 0)
		{
			// Root reference
			resolved = ctx.RootSchema;
		}
		else if (parts.Length >= 2 && (parts[0] == "$defs" || parts[0] == "definitions"))
		{
			var defKey = parts[1];
			if (ctx.Defs.TryGetValue(defKey, out var def))
			{
				resolved = def;
			}
		}

		if (resolved == null)
		{
			throw new ArgumentException($"Reference not found: {refPath}");
		}

		var zodSchema = ConvertSchema(resolved, ctx);
		ctx.Refs[refPath] = zodSchema;
		ctx.Processing.Remove(refPath);

		return zodSchema;
	}

	static IZodSchema<object, object> CreateLiteralSchema(object? value)
	{
		// Handle JToken (from Newtonsoft.Json)
		if (value is JToken jToken)
		{
			return CreateLiteralFromJToken(jToken);
		}

		// Handle primitive types
		return value switch
		{
			string s => new LiteralSchemaWrapper<string>(s),
			int i => new LiteralSchemaWrapper<int>(i),
			long l => new LiteralSchemaWrapper<long>(l),
			double d => new LiteralSchemaWrapper<double>(d),
			float f => new LiteralSchemaWrapper<float>(f),
			bool b => new LiteralSchemaWrapper<bool>(b),
			null => new NullSchemaWrapper(),
			_ => new AnySchema(),
		};
	}

	[System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0072:Add missing cases")]
	static IZodSchema<object, object> CreateLiteralFromJToken(JToken token)
	{
		return token.Type switch
		{
			JTokenType.String => new LiteralSchemaWrapper<string>(token.Value<string>()!),
			JTokenType.Integer => new LiteralSchemaWrapper<long>(token.Value<long>()),
			JTokenType.Float => new LiteralSchemaWrapper<double>(token.Value<double>()),
			JTokenType.Boolean => new LiteralSchemaWrapper<bool>(token.Value<bool>()),
			JTokenType.Null => new NullSchemaWrapper(),
			_ => new AnySchema(),
		};
	}

	// ========== Wrapper Classes ==========

	sealed class StringSchemaWrapper(ZodString inner) : IZodSchema<object, object>
	{
		public ValidationResult<object> Validate(object value)
		{
			if (value is not string str)
			{
				// Handle JToken
				if (value is JToken jt && jt.Type == JTokenType.String)
				{
					str = jt.Value<string>()!;
				}
				else
				{
					return ValidationResult<object>.Failure(
						new ValidationError(
							"invalid_type",
							$"Expected string, but got {value?.GetType().Name ?? "null"}",
							[]
						)
					);
				}
			}

			var result = inner.Validate(str);
			return result.IsSuccess
				? ValidationResult<object>.Success(result.Value!)
				: ValidationResult<object>.Failure(result.Errors);
		}

		public ValueTask<ValidationResult<object>> ValidateAsync(
			object value,
			CancellationToken cancellationToken = default
		) => new(Validate(value));
	}

	class NumberSchemaWrapper(ZodNumber inner) : IZodSchema<object, object>
	{
		public ValidationResult<object> Validate(object value)
		{
			double num;
			if (value is double d)
				num = d;
			else if (value is int i)
				num = i;
			else if (value is long l)
				num = l;
			else if (value is float f)
				num = f;
			else if (value is JToken jt && (jt.Type == JTokenType.Integer || jt.Type == JTokenType.Float))
			{
				num = jt.Value<double>();
			}
			else
			{
				return ValidationResult<object>.Failure(
					new ValidationError(
						"invalid_type",
						$"Expected number, but got {value?.GetType().Name ?? "null"}",
						[]
					)
				);
			}

			var result = inner.Validate(num);
			return result.IsSuccess
				? ValidationResult<object>.Success(result.Value)
				: ValidationResult<object>.Failure(result.Errors);
		}

		public ValueTask<ValidationResult<object>> ValidateAsync(
			object value,
			CancellationToken cancellationToken = default
		) => new(Validate(value));
	}

	class BooleanSchemaWrapper : IZodSchema<object, object>
	{
		readonly ZodBoolean _inner = Z.Boolean();

		public ValidationResult<object> Validate(object value)
		{
			bool b;
			if (value is bool boolVal)
			{
				b = boolVal;
			}
			else if (value is JToken jt && jt.Type == JTokenType.Boolean)
			{
				b = jt.Value<bool>();
			}
			else
			{
				return ValidationResult<object>.Failure(
					new ValidationError(
						"invalid_type",
						$"Expected boolean, but got {value?.GetType().Name ?? "null"}",
						[]
					)
				);
			}

			var result = _inner.Validate(b);
			return result.IsSuccess
				? ValidationResult<object>.Success(result.Value)
				: ValidationResult<object>.Failure(result.Errors);
		}

		public ValueTask<ValidationResult<object>> ValidateAsync(
			object value,
			CancellationToken cancellationToken = default
		) => new(Validate(value));
	}

	class NullSchemaWrapper : IZodSchema<object, object>
	{
		public ValidationResult<object> Validate(object value)
		{
			if (value != null && !(value is JToken jt && jt.Type == JTokenType.Null))
			{
				return ValidationResult<object>.Failure(
					new ValidationError("invalid_type", $"Expected null, but got {value.GetType().Name}", [])
				);
			}

			// If matches, return success with null
			return ValidationResult<object>.Success(null!);
		}

		public ValueTask<ValidationResult<object>> ValidateAsync(
			object value,
			CancellationToken cancellationToken = default
		) => new(Validate(value));
	}

	class ObjectSchemaWrapper(ZodObject inner) : IZodSchema<object, object>
	{
		public ValidationResult<object> Validate(object value)
		{
			Dictionary<string, object?> dict;

			if (value is Dictionary<string, object?> d)
			{
				dict = d;
			}
			else if (value is JObject jo)
			{
				dict = [];
				foreach (var prop in jo.Properties())
				{
					dict[prop.Name] = ConvertJToken(prop.Value);
				}
			}
			else
			{
				return ValidationResult<object>.Failure(
					new ValidationError(
						"invalid_type",
						$"Expected object, but got {value?.GetType().Name ?? "null"}",
						[]
					)
				);
			}

			var result = inner.Validate(dict);
			return result.IsSuccess
				? ValidationResult<object>.Success(result.Value)
				: ValidationResult<object>.Failure(result.Errors);
		}

		public ValueTask<ValidationResult<object>> ValidateAsync(
			object value,
			CancellationToken cancellationToken = default
		) => new(Validate(value));

		[System.Diagnostics.CodeAnalysis.SuppressMessage(
			"Performance",
			"CA1859:Use concrete types when possible for improved performance"
		)]
		[System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0072:Add missing cases")]
		static object? ConvertJToken(JToken token)
		{
			return token.Type switch
			{
				JTokenType.String => token.Value<string>(),
				JTokenType.Integer => token.Value<long>(),
				JTokenType.Float => token.Value<double>(),
				JTokenType.Boolean => token.Value<bool>(),
				JTokenType.Null => null,
				JTokenType.Object => token,
				JTokenType.Array => token,
				_ => token,
			};
		}
	}

	class ArraySchemaWrapper(IZodSchema<object, object> elementSchema, int? minItems, int? maxItems)
		: IZodSchema<object, object>
	{
		public ValidationResult<object> Validate(object value)
		{
			List<object?> items;

			if (value is JArray jArray)
			{
				items = [.. jArray.Cast<object?>()];
			}
			else if (value is System.Collections.IEnumerable enumerable and not string)
			{
				items = [.. enumerable.Cast<object?>()];
			}
			else
			{
				return ValidationResult<object>.Failure(
					new ValidationError(
						"invalid_type",
						$"Expected array, but got {value?.GetType().Name ?? "null"}",
						[]
					)
				);
			}

			// Check length constraints
			if (minItems.HasValue && items.Count < minItems.Value)
			{
				return ValidationResult<object>.Failure(
					new ValidationError("too_small", $"Array must contain at least {minItems.Value} items", [])
				);
			}

			if (maxItems.HasValue && items.Count > maxItems.Value)
			{
				return ValidationResult<object>.Failure(
					new ValidationError("too_big", $"Array must contain at most {maxItems.Value} items", [])
				);
			}

			// Validate each item
			List<ValidationError> errors = [];
			List<object?> validatedItems = [];

			for (var i = 0; i < items.Count; i++)
			{
				var itemResult = elementSchema.Validate(items[i]!);
				if (itemResult.IsSuccess)
				{
					validatedItems.Add(itemResult.Value);
				}
				else
				{
					foreach (var error in itemResult.Errors)
					{
						var path = new string[error.Path.Length + 1];
						path[0] = i.ToString(CultureInfo.InvariantCulture);
						error.Path.CopyTo(0, path, 1, error.Path.Length);
						errors.Add(new ValidationError(error.Code, error.Message, path, error.Parameters));
					}
				}
			}

			if (errors.Count > 0)
			{
				return ValidationResult<object>.Failure(errors);
			}

			// Return the validated items as a list
			return ValidationResult<object>.Success(validatedItems);
		}

		public ValueTask<ValidationResult<object>> ValidateAsync(
			object value,
			CancellationToken cancellationToken = default
		) => new(Validate(value));
	}

	class OptionalSchemaWrapper(IZodSchema<object, object> inner) : IZodSchema<object, object>
	{
		public ValidationResult<object> Validate(object value)
		{
			if (value == null)
				return ValidationResult<object>.Success(null!);
			if (value is JToken jt && jt.Type == JTokenType.Null)
				return ValidationResult<object>.Success(null!);

			// Validate using the inner schema
			return inner.Validate(value);
		}

		public ValueTask<ValidationResult<object>> ValidateAsync(
			object value,
			CancellationToken cancellationToken = default
		) => new(Validate(value));
	}

	class LazySchemaWrapper(Func<IZodSchema<object, object>> getter) : IZodSchema<object, object>
	{
		public ValidationResult<object> Validate(object value) => getter().Validate(value);

		public ValueTask<ValidationResult<object>> ValidateAsync(
			object value,
			CancellationToken cancellationToken = default
		) => getter().ValidateAsync(value, cancellationToken);
	}

	class LiteralSchemaWrapper<T>(T value) : IZodSchema<object, object>
		where T : IEquatable<T>
	{
		readonly T _value = value;

		public ValidationResult<object> Validate(object value)
		{
			T? typedValue = default;
			var matches = false;

			if (value is T t)
			{
				typedValue = t;
				matches = _value.Equals(t);
			}
			else if (value is JToken jt)
			{
				// Handle JToken
#pragma warning disable IDE0072 // Add missing cases
				object? extracted = jt.Type switch
				{
					JTokenType.String => jt.Value<string>(),
					JTokenType.Integer when typeof(T) == typeof(int) => jt.Value<int>(),
					JTokenType.Integer when typeof(T) == typeof(long) => jt.Value<long>(),
					JTokenType.Float when typeof(T) == typeof(double) => jt.Value<double>(),
					JTokenType.Boolean => jt.Value<bool>(),
					_ => null,
				};
#pragma warning restore IDE0072 // Add missing cases

				if (extracted is T ext)
				{
					typedValue = ext;
					matches = _value.Equals(ext);
				}
			}

			if (!matches)
			{
				return ValidationResult<object>.Failure(
					new ValidationError("invalid_literal", $"Expected literal value '{_value}', but got '{value}'", [])
				);
			}

			// If matches, return success with the typed value
			return ValidationResult<object>.Success(typedValue!);
		}

		public ValueTask<ValidationResult<object>> ValidateAsync(
			object value,
			CancellationToken cancellationToken = default
		) => new(Validate(value));
	}

	class AnySchema : IZodSchema<object, object>
	{
		public ValidationResult<object> Validate(object value) => ValidationResult<object>.Success(value);

		public ValueTask<ValidationResult<object>> ValidateAsync(
			object value,
			CancellationToken cancellationToken = default
		) => new(ValidationResult<object>.Success(value));
	}
}
